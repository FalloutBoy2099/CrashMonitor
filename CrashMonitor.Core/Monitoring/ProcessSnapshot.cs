namespace CrashMonitor.Core.Monitoring
{
	using System;

	public sealed record ProcessSnapshot(
		Int32 ProcessId,
		String ProcessName,
		String? MainModulePath,
		String? WorkingDirectory,
		DateTimeOffset StartedAt);
}