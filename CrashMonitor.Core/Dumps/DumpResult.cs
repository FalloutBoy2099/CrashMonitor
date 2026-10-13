namespace CrashMonitor.Core.Dumps
{
	using System;

	public sealed record DumpResult(String Path, String DumpType, Int64 SizeBytes);
}