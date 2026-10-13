namespace CrashMonitor.Analyzer.KnownErrors
{
	using System;

	public sealed record KnownErrorMatch(KnownErrorEntry Entry, Int64 Distance);
}