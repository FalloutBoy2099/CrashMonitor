namespace CrashMonitor.Cli
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;
	using CrashMonitor.Analyzer;
	using CrashMonitor.Analyzer.KnownErrors;
	using CrashMonitor.Analyzer.Scripts;
	using CrashMonitor.Core;
	using CrashMonitor.Core.Configuration;
	using CrashMonitor.Core.Context;
	using CrashMonitor.Core.Dumps;
	using CrashMonitor.Core.Monitoring;
	using CrashMonitor.Core.SfallDebug;
	using GitHubIssues;
	using Triage;

	internal static class Program
	{
		private const Int32 Success = 0;
		private const Int32 Failure = 1;

		public static async Task<Int32> Main(String[] args)
		{
			if (args.Length == 1 && args[0].Equals("-debugedit", StringComparison.OrdinalIgnoreCase))
			{
				return SfallDebug(["--out", $"sfall-debug-snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.md"]);
			}

			if (args.Length == 0 || IsHelp(args[0]))
			{
				PrintHelp();
				return Success;
			}

			try
			{
				return args[0].ToLowerInvariant() switch
				{
					"watch" => await WatchAsync(args[1..]),
					"dump" => Dump(args[1..]),
					"analyze" => Analyze(args[1..]),
					"init-config" => InitConfig(args[1..]),
					"init-db" => InitDatabase(args[1..]),
					"import-github-issues" => await ImportGitHubIssuesAsync(args[1..]),
					"triage" => Triage(args[1..]),
					"analyze-script" => AnalyzeScript(args[1..]),
					"sfall-debug" => SfallDebug(args[1..]),
					_ => UnknownCommand(args[0])
				};
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine("Stopped.");
				return Success;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex.Message);
				return Failure;
			}
		}

		private static async Task<Int32> WatchAsync(String[] args)
		{
			var options = CliOptions.Parse(args);
			var config = LoadConfig(options);
			ApplyOverrides(config, options);

			var watcher = CreateWatcher(config);
			using var cancellation = new CancellationTokenSource();
			Console.CancelKeyPress += (_, eventArgs) =>
			{
				eventArgs.Cancel = true;
				cancellation.Cancel();
			};

			await watcher.WatchAsync(Console.WriteLine, cancellation.Token);
			return Success;
		}

		private static Int32 Dump(String[] args)
		{
			var options = CliOptions.Parse(args);
			if (!options.TryGetInt32("pid", out var processId))
			{
				Console.Error.WriteLine("Missing required option: --pid <processId>");
				return Failure;
			}

			var config = LoadConfig(options);
			ApplyOverrides(config, options);
			var finder = new ProcessFinder();
			using var process = finder.FindById(processId);
			if (process is null)
			{
				Console.Error.WriteLine($"Process not found: {processId}");
				return Failure;
			}

			var watcher = CreateWatcher(config);
			var crashEvent = watcher.WriteManualDump(process, Console.WriteLine);
			Console.WriteLine($"Capture: {crashEvent.CaptureDirectory}");
			return crashEvent.Dump is null ? Failure : Success;
		}

		private static Int32 Analyze(String[] args)
		{
			if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
			{
				Console.Error.WriteLine("Missing dump path.");
				return Failure;
			}

			var dumpPath = args[0];
			var options = CliOptions.Parse(args[1..]);
			var knownErrorsPath = options.Get("known-errors") ?? options.Get("db") ?? "known-errors.json";
			var contextPath = options.Get("context");
			var outputPath = options.Get("out");

			var analyzer = new CrashAnalyzer();
			var result = analyzer.Analyze(dumpPath, knownErrorsPath, contextPath);
			if (String.IsNullOrWhiteSpace(outputPath))
			{
				Console.WriteLine(result.Markdown);
			}
			else
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
				File.WriteAllText(outputPath, result.Markdown);
				Console.WriteLine($"Report written: {Path.GetFullPath(outputPath)}");
			}

			return Success;
		}

		private static Int32 InitConfig(String[] args)
		{
			var path = args.Length == 0 ? "crashmonitor.config.json" : args[0];
			ConfigStore.Save(path, new CrashMonitorConfig());
			Console.WriteLine($"Config written: {Path.GetFullPath(path)}");
			return Success;
		}

		private static Int32 InitDatabase(String[] args)
		{
			var path = args.Length == 0 ? "known-errors.json" : args[0];
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
			File.WriteAllText(path, SampleKnownErrors.Json);
			Console.WriteLine($"Known errors database written: {Path.GetFullPath(path)}");
			return Success;
		}

		private static async Task<Int32> ImportGitHubIssuesAsync(String[] args)
		{
			var options = CliOptions.Parse(args);
			var outputPath = options.Get("out") ?? "known-errors.github.json";
			var importOptions = new GitHubIssueImportOptions
			{
				Repository = options.Get("repo") ?? "egornovivan/CrashMonitor",
				State = options.Get("state") ?? "all",
				MaxPages = options.TryGetInt32("max-pages", out var maxPages) ? Math.Max(1, maxPages) : 5,
				MinAddress = ParseAddress(options.Get("min-address") ?? "0x00400000"),
				IncludeLowAddresses = options.Has("include-low-addresses"),
				OnlyFalloutExe = options.Has("only-fallout-exe")
			};

			using var client = new HttpClient();
			client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CrashMonitor", "1.0"));
			if (Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { Length: > 0 } token)
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
			}

			var importer = new GitHubIssueKnownErrorImporter(client);
			var importedEntries = await importer.ImportAsync(importOptions, CancellationToken.None);
			var entries = MergeEntries(options.Get("merge"), importedEntries);

			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
			File.WriteAllText(outputPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			}));

			Console.WriteLine($"Imported entries: {importedEntries.Count}");
			Console.WriteLine($"Written entries: {entries.Count}");
			Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");
			return Success;
		}

		private static Int32 Triage(String[] args)
		{
			if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
			{
				Console.Error.WriteLine("Missing known errors JSON path.");
				return Failure;
			}

			var path = args[0];
			var options = CliOptions.Parse(args[1..]);
			var limit = options.TryGetInt32("limit", out var parsedLimit) ? Math.Max(1, parsedLimit) : 30;
			var outputPath = options.Get("out");
			var entries = LoadKnownErrorEntries(path);
			var report = new KnownErrorTriageReport().BuildMarkdown(entries, limit);

			if (String.IsNullOrWhiteSpace(outputPath))
			{
				Console.WriteLine(report);
			}
			else
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
				File.WriteAllText(outputPath, report);
				Console.WriteLine($"Triage report written: {Path.GetFullPath(outputPath)}");
			}

			return Success;
		}

		private static Int32 AnalyzeScript(String[] args)
		{
			if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
			{
				Console.Error.WriteLine("Missing INT script path.");
				return Failure;
			}

			var scriptPath = args[0];
			var options = CliOptions.Parse(args[1..]);
			var outputPath = options.Get("out");
			var unsafeAddress = options.Get("unsafe-address") is { } addressText ? ParseAddress(addressText) : (UInt32?)null;
			var analysis = new IntScriptAnalyzer().Analyze(scriptPath);
			var report = new IntScriptReportBuilder().Build(analysis, unsafeAddress);

			if (String.IsNullOrWhiteSpace(outputPath))
			{
				Console.WriteLine(report);
			}
			else
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
				File.WriteAllText(outputPath, report);
				Console.WriteLine($"Script report written: {Path.GetFullPath(outputPath)}");
			}

			return Success;
		}

		private static Int32 SfallDebug(String[] args)
		{
			var options = CliOptions.Parse(args);
			String host = options.Get("host") ?? SfallDebugClient.DefaultHost;
			Int32 port = options.TryGetInt32("port", out Int32 parsedPort) ? parsedPort : SfallDebugClient.DefaultPort;
			Int32 delay = options.TryGetInt32("delay-ms", out Int32 parsedDelay) ? Math.Max(0, parsedDelay) : 1000;
			Int32 maxRows = options.TryGetInt32("max-rows", out Int32 parsedMaxRows) ? Math.Max(1, parsedMaxRows) : 100;
			String? outputPath = options.Get("out");

			using SfallDebugClient client = SfallDebugClient.Connect(host, port, delay);
			SfallDebugSnapshot snapshot = client.ReadSnapshot();

			Dictionary<Int32, IReadOnlyList<SfallDebugArrayItem>> arrayValues = new();
			foreach (Int32 arrayIndex in ParseIndexList(options.Get("arrays") ?? options.Get("array")))
			{
				if (arrayIndex >= 0 && arrayIndex < snapshot.Arrays.Count)
				{
					arrayValues[arrayIndex] = client.GetArrayValues(arrayIndex, snapshot.Arrays[arrayIndex]);
				}
			}

			Dictionary<Int32, SfallDebugLocalVariables> localVariables = new();
			foreach (Int32 scriptId in ParseIndexList(options.Get("locals")))
			{
				localVariables[scriptId] = client.GetLocalVariables(scriptId);
			}

			foreach (Int32 critterIndex in ParseIndexList(options.Get("critter-locals")))
			{
				if (critterIndex >= 0 && critterIndex < snapshot.Critters.Count)
				{
					SfallDebugCritterDetails details = client.GetCritterDetails(critterIndex);
					if (details.RawFields.Count > 0 && details.RawFields[0] > 0)
					{
						localVariables[details.RawFields[0]] = client.GetLocalVariables(details.RawFields[0]);
					}
				}
			}

			String report = new SfallDebugReportBuilder().Build(snapshot, arrayValues, localVariables, maxRows);
			client.Exit();

			if (String.IsNullOrWhiteSpace(outputPath))
			{
				Console.WriteLine(report);
			}
			else
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
				File.WriteAllText(outputPath, report);
				Console.WriteLine($"sfall debug snapshot written: {Path.GetFullPath(outputPath)}");
			}

			return Success;
		}

		private static IReadOnlyList<KnownErrorEntry> MergeEntries(String? existingPath, IReadOnlyList<KnownErrorEntry> importedEntries)
		{
			if (String.IsNullOrWhiteSpace(existingPath) || !File.Exists(existingPath))
			{
				return importedEntries;
			}

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			var existingEntries = JsonSerializer.Deserialize<List<KnownErrorEntry>>(File.ReadAllText(existingPath), options) ?? [];
			var knownKeys = new HashSet<String>(
				existingEntries.Select(entry => $"{entry.Reference}|{entry.Address}|{entry.ObservedMemoryAddress}"),
				StringComparer.OrdinalIgnoreCase);

			foreach (var entry in importedEntries)
			{
				var key = $"{entry.Reference}|{entry.Address}|{entry.ObservedMemoryAddress}";
				if (knownKeys.Add(key))
				{
					existingEntries.Add(entry);
				}
			}

			return existingEntries;
		}

		private static IReadOnlyList<KnownErrorEntry> LoadKnownErrorEntries(String path)
		{
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			return JsonSerializer.Deserialize<List<KnownErrorEntry>>(File.ReadAllText(path), options) ?? [];
		}

		private static UInt32 ParseAddress(String value)
		{
			var clean = value.Trim();
			if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				clean = clean[2..];
			}

			return UInt32.Parse(clean, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
		}

		private static IReadOnlyList<Int32> ParseIndexList(String? value)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return [];
			}

			List<Int32> values = new();
			foreach (String item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (Int32.TryParse(item, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out Int32 parsed))
				{
					values.Add(parsed);
				}
			}

			return values;
		}

		private static CrashMonitorConfig LoadConfig(CliOptions options)
		{
			return ConfigStore.LoadOrDefault(options.Get("config"));
		}

		private static void ApplyOverrides(CrashMonitorConfig config, CliOptions options)
		{
			if (options.Get("fallout-dir") is { } falloutDirectory)
			{
				config.FalloutDirectory = falloutDirectory;
			}

			if (options.Get("output") is { } outputDirectory)
			{
				config.OutputDirectory = outputDirectory;
			}

			if (options.Get("process") is { } processName)
			{
				config.ProcessName = Path.GetFileNameWithoutExtension(processName);
			}

			if (options.Get("dump-kind") is { } dumpKind && Enum.TryParse<DumpKind>(dumpKind, true, out var parsedDumpKind))
			{
				config.DumpKind = parsedDumpKind;
			}
		}

		private static FalloutProcessWatcher CreateWatcher(CrashMonitorConfig config)
		{
			return new FalloutProcessWatcher(
				config,
				new ProcessFinder(),
				new MiniDumpWriter(),
				new ContextCollector(config),
				new RetentionService());
		}

		private static Boolean IsHelp(String value)
		{
			return value.Equals("-h", StringComparison.OrdinalIgnoreCase)
				|| value.Equals("--help", StringComparison.OrdinalIgnoreCase)
				|| value.Equals("help", StringComparison.OrdinalIgnoreCase);
		}

		private static Int32 UnknownCommand(String command)
		{
			Console.Error.WriteLine($"Unknown command: {command}");
			PrintHelp();
			return Failure;
		}

		private static void PrintHelp()
		{
			Console.WriteLine("CrashMonitor");
			Console.WriteLine();
			Console.WriteLine("Usage:");
			Console.WriteLine("  CrashMonitor.Cli watch [--config path] [--fallout-dir path] [--output path] [--process fallout2] [--dump-kind Normal]");
			Console.WriteLine("  CrashMonitor.Cli dump --pid processId [--config path] [--output path] [--dump-kind Normal]");
			Console.WriteLine("  CrashMonitor.Cli analyze dumpPath [--known-errors path] [--context path] [--out path]");
			Console.WriteLine("  CrashMonitor.Cli init-config [path]");
			Console.WriteLine("  CrashMonitor.Cli init-db [path]");
			Console.WriteLine("  CrashMonitor.Cli import-github-issues [--repo owner/name] [--state all] [--max-pages 5] [--min-address 0x00400000] [--include-low-addresses] [--only-fallout-exe] [--merge known-errors.json] [--out known-errors.github.json]");
			Console.WriteLine("  CrashMonitor.Cli triage known-errors.json [--limit 30] [--out triage.md]");
			Console.WriteLine("  CrashMonitor.Cli analyze-script scriptPath [--unsafe-address 0x004124E0] [--out script-analysis.md]");
			Console.WriteLine("  CrashMonitor.Cli sfall-debug [--host 127.0.0.1] [--port 4245] [--arrays 0,1] [--locals 123] [--critter-locals 0] [--max-rows 100] [--out snapshot.md]");
		}
	}
}