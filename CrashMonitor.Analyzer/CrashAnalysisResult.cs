namespace CrashMonitor.Analyzer
{
	using System;
	using KnownErrors;
	using Minidumps;

	public sealed record CrashAnalysisResult(MinidumpAnalysis Dump, KnownErrorMatch? KnownError, String Markdown);
}