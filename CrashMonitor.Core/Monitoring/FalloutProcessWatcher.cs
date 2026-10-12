namespace CrashMonitor.Core.Monitoring
{
	using System.Diagnostics;
	using CrashMonitor.Core.Configuration;
	using CrashMonitor.Core.Context;
	using CrashMonitor.Core.Dumps;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;

	public sealed class FalloutProcessWatcher
	{
		private readonly CrashMonitorConfig _config;
		private readonly ProcessFinder _finder;
		private readonly MiniDumpWriter _dumpWriter;
		private readonly ContextCollector _contextCollector;
		private readonly RetentionService _retentionService;

		public FalloutProcessWatcher(
			CrashMonitorConfig config,
			ProcessFinder finder,
			MiniDumpWriter dumpWriter,
			ContextCollector contextCollector,
			RetentionService retentionService)
		{
			_config = config;
			_finder = finder;
			_dumpWriter = dumpWriter;
			_contextCollector = contextCollector;
			_retentionService = retentionService;
		}

		public async Task WatchAsync(Action<String> log, CancellationToken cancellationToken)
		{
			log($"Waiting for {_config.ProcessName}.exe...");
			while (!cancellationToken.IsCancellationRequested)
			{
				var process = _finder.FindFirst(_config.ProcessName);
				if (process is null)
				{
					await Task.Delay(_config.PollIntervalMilliseconds, cancellationToken);
					continue;
				}

				await WatchProcessAsync(process, log, cancellationToken);
			}
		}

		public CrashEvent WriteManualDump(Process process, Action<String> log)
		{
			return Capture(process, CrashReason.ManualDump, null, log);
		}

		private async Task WatchProcessAsync(Process process, Action<String> log, CancellationToken cancellationToken)
		{
			var snapshot = CreateSnapshot(process);
			log($"Attached to {snapshot.ProcessName}.exe PID {snapshot.ProcessId}.");
			DateTimeOffset? notRespondingSince = null;
			var dumpedFreeze = false;

			while (!process.HasExited && !cancellationToken.IsCancellationRequested)
			{
				if (_config.DetectNotResponding && process.MainWindowHandle != IntPtr.Zero)
				{
					process.Refresh();
					if (!process.Responding)
					{
						notRespondingSince ??= DateTimeOffset.Now;
						var frozenFor = DateTimeOffset.Now - notRespondingSince.Value;
						if (!dumpedFreeze && frozenFor.TotalSeconds >= _config.NotRespondingSeconds)
						{
							log($"Process not responding for {(Int32)frozenFor.TotalSeconds}s; writing dump.");
							Capture(process, CrashReason.NotResponding, null, log);
							dumpedFreeze = true;
						}
					}
					else
					{
						notRespondingSince = null;
						dumpedFreeze = false;
					}
				}

				await Task.Delay(_config.PollIntervalMilliseconds, cancellationToken);
			}

			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			var exitCode = process.ExitCodeSafe();
			if (exitCode.GetValueOrDefault() != 0)
			{
				log($"Process exited with code {exitCode}; collecting context without live dump.");
				var captureDirectory = CreateCaptureDirectory(snapshot, CrashReason.ProcessExitedUnexpectedly);
				_contextCollector.Collect(snapshot, captureDirectory);
				_retentionService.Apply(_config.OutputDirectory, _config.KeepLastDumps);
			}
			else
			{
				log("Process exited normally.");
			}
		}

		private CrashEvent Capture(Process process, CrashReason reason, Int32? exitCode, Action<String> log)
		{
			var snapshot = CreateSnapshot(process);
			var captureDirectory = CreateCaptureDirectory(snapshot, reason);
			var dumpPath = Path.Combine(captureDirectory, $"{Path.GetFileNameWithoutExtension(snapshot.ProcessName)}_{DateTime.Now:yyyyMMdd_HHmmss}.dmp");

			DumpResult? dump = null;
			try
			{
				dump = _dumpWriter.Write(process, dumpPath, _config.DumpKind);
				log($"Dump written: {dump.Path} ({dump.SizeBytes:N0} bytes).");
			}
			catch (Exception ex)
			{
				log($"Dump failed: {ex.Message}");
			}

			_contextCollector.Collect(snapshot, captureDirectory);
			_retentionService.Apply(_config.OutputDirectory, _config.KeepLastDumps);
			return new CrashEvent(snapshot, reason, exitCode, dump, captureDirectory, DateTimeOffset.Now);
		}

		private String CreateCaptureDirectory(ProcessSnapshot snapshot, CrashReason reason)
		{
			var root = Path.GetFullPath(_config.OutputDirectory);
			var name = $"{DateTime.Now:yyyyMMdd_HHmmss}_{snapshot.ProcessName}_{reason}";
			var path = Path.Combine(root, name);
			Directory.CreateDirectory(path);
			return path;
		}

		private ProcessSnapshot CreateSnapshot(Process process)
		{
			var modulePath = process.MainModuleFileNameSafe();
			var workingDirectory = _config.FalloutDirectory;
			if (String.IsNullOrWhiteSpace(workingDirectory) && !String.IsNullOrWhiteSpace(modulePath))
			{
				workingDirectory = Path.GetDirectoryName(modulePath);
			}

			return new ProcessSnapshot(
				process.Id,
				process.ProcessName,
				modulePath,
				workingDirectory,
				process.StartTimeSafe() ?? DateTime.Now);
		}
	}
}