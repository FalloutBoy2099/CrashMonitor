namespace CrashMonitor.Core.Monitoring
{
	using System;
	using CrashMonitor.Core.Dumps;

	public sealed record CrashEvent(
		ProcessSnapshot Process,
		CrashReason Reason,
		Int32? ExitCode,
		DumpResult? Dump,
		String CaptureDirectory,
		DateTimeOffset OccurredAt);
}