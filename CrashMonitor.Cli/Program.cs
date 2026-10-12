namespace CrashMonitor.Cli
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using CrashMonitor.Analyzer;
	using CrashMonitor.Core;
	using CrashMonitor.Core.Configuration;
	using CrashMonitor.Core.Context;
	using CrashMonitor.Core.Dumps;
	using CrashMonitor.Core.Monitoring;

	internal static class Program
	{
		private const Int32 Success = 0;
		private const Int32 Failure = 1;

		public static async Task<Int32> Main(String[] args)
		{
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
		}
	}
}