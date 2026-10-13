namespace CrashMonitor.Cli.Triage
{
	using System;

	internal sealed record KnownErrorTriageGroup(
		String Address,
		Int32 Count,
		String? Module,
		String Status,
		String MemoryReferences,
		String Issues);
}