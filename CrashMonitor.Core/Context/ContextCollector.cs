namespace CrashMonitor.Core.Context
{
	using System.Runtime.InteropServices;
	using System.Text;
	using CrashMonitor.Core.Configuration;
	using CrashMonitor.Core.Monitoring;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;

	public sealed class ContextCollector
	{
		private readonly CrashMonitorConfig _config;

		public ContextCollector(CrashMonitorConfig config)
		{
			_config = config;
		}

		public ContextResult Collect(ProcessSnapshot process, String captureDirectory)
		{
			Directory.CreateDirectory(captureDirectory);
			var falloutDirectory = ResolveFalloutDirectory(process);
			var copiedFiles = new List<String>();

			if (_config.CopyLogs && falloutDirectory is not null)
			{
				copiedFiles.AddRange(CopyKnownFiles(falloutDirectory, captureDirectory));
			}

			if (_config.CopyRecentSaves && falloutDirectory is not null)
			{
				copiedFiles.AddRange(CopyRecentSaves(falloutDirectory, captureDirectory));
			}

			var contextPath = Path.Combine(captureDirectory, "context.md");
			File.WriteAllText(contextPath, BuildContextMarkdown(process, falloutDirectory, copiedFiles), Encoding.UTF8);
			return new ContextResult(captureDirectory, contextPath, copiedFiles);
		}

		private String? ResolveFalloutDirectory(ProcessSnapshot process)
		{
			if (!String.IsNullOrWhiteSpace(_config.FalloutDirectory))
			{
				return Path.GetFullPath(_config.FalloutDirectory);
			}

			return process.WorkingDirectory;
		}

		private static IEnumerable<String> CopyKnownFiles(String falloutDirectory, String captureDirectory)
		{
			var copied = new List<String>();
			var contextFiles = Path.Combine(captureDirectory, "files");
			Directory.CreateDirectory(contextFiles);

			var fixedFiles = new[]
			{
				"debug.log",
				"ddraw.ini",
				"f2_res.ini",
				"fallout2.cfg",
				"version.txt",
				"mods_order.txt",
				Path.Combine("mods", "mods_order.ini")
			};

			foreach (var relative in fixedFiles)
			{
				var source = Path.Combine(falloutDirectory, relative);
				if (File.Exists(source))
				{
					copied.Add(CopyFile(falloutDirectory, contextFiles, source));
				}
			}

			foreach (var source in Directory.EnumerateFiles(falloutDirectory, "sfall-log*", SearchOption.TopDirectoryOnly))
			{
				copied.Add(CopyFile(falloutDirectory, contextFiles, source));
			}

			var modsDirectory = Path.Combine(falloutDirectory, "mods");
			if (Directory.Exists(modsDirectory))
			{
				foreach (var source in Directory.EnumerateFiles(modsDirectory, "*.ini", SearchOption.TopDirectoryOnly))
				{
					copied.Add(CopyFile(falloutDirectory, contextFiles, source));
				}
			}

			return copied;
		}

		private IEnumerable<String> CopyRecentSaves(String falloutDirectory, String captureDirectory)
		{
			var saveRoot = Path.Combine(falloutDirectory, "data", "SAVEGAME");
			if (!Directory.Exists(saveRoot))
			{
				return [];
			}

			var targetRoot = Path.Combine(captureDirectory, "saves");
			Directory.CreateDirectory(targetRoot);

			var copied = new List<String>();
			foreach (var slot in Directory.EnumerateDirectories(saveRoot)
				.OrderByDescending(Directory.GetLastWriteTimeUtc)
				.Take(Math.Max(1, _config.RecentSaveCount)))
			{
				var target = Path.Combine(targetRoot, Path.GetFileName(slot));
				CopyDirectory(slot, target);
				copied.Add(target);
			}

			return copied;
		}

		private static String CopyFile(String root, String targetRoot, String source)
		{
			var relative = Path.GetRelativePath(root, source);
			var target = Path.Combine(targetRoot, relative);
			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			File.Copy(source, target, overwrite: true);
			return target;
		}

		private static void CopyDirectory(String source, String target)
		{
			Directory.CreateDirectory(target);
			foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
			{
				Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
			}

			foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
			{
				File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: true);
			}
		}

		private static String BuildContextMarkdown(ProcessSnapshot process, String? falloutDirectory, IReadOnlyCollection<String> copiedFiles)
		{
			var builder = new StringBuilder();
			builder.AppendLine("# Crash Context");
			builder.AppendLine();
			builder.AppendLine($"- Process: `{process.ProcessName}`");
			builder.AppendLine($"- PID: `{process.ProcessId}`");
			builder.AppendLine($"- Started: `{process.StartedAt:O}`");
			builder.AppendLine($"- Executable: `{process.MainModulePath ?? "unknown"}`");
			builder.AppendLine($"- Fallout directory: `{falloutDirectory ?? "unknown"}`");
			builder.AppendLine($"- OS: `{RuntimeInformation.OSDescription}`");
			builder.AppendLine($"- Framework: `{RuntimeInformation.FrameworkDescription}`");
			builder.AppendLine($"- Architecture: `{RuntimeInformation.ProcessArchitecture}`");

			if (falloutDirectory is not null)
			{
				AppendFileSummary(builder, falloutDirectory, "ddraw.ini", "sfall / ddraw");
				AppendFileSummary(builder, falloutDirectory, "version.txt", "Version");
				AppendMods(builder, falloutDirectory);
			}

			builder.AppendLine();
			builder.AppendLine("## Copied Files");
			if (copiedFiles.Count == 0)
			{
				builder.AppendLine("- none");
			}
			else
			{
				foreach (var copiedFile in copiedFiles)
				{
					builder.AppendLine($"- `{copiedFile}`");
				}
			}

			return builder.ToString();
		}

		private static void AppendFileSummary(StringBuilder builder, String root, String fileName, String label)
		{
			var file = Path.Combine(root, fileName);
			if (!File.Exists(file))
			{
				return;
			}

			builder.AppendLine();
			builder.AppendLine($"## {label}");
			foreach (var line in File.ReadLines(file).Take(40))
			{
				builder.AppendLine($"> {line}");
			}
		}

		private static void AppendMods(StringBuilder builder, String falloutDirectory)
		{
			var modsDirectory = Path.Combine(falloutDirectory, "mods");
			if (!Directory.Exists(modsDirectory))
			{
				return;
			}

			builder.AppendLine();
			builder.AppendLine("## Mods");
			foreach (var entry in Directory.EnumerateFileSystemEntries(modsDirectory).Order())
			{
				builder.AppendLine($"- `{Path.GetFileName(entry)}`");
			}
		}
	}
}