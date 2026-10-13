namespace CrashMonitor.Analyzer
{
	using System;
	using KnownErrors;
	using Minidumps;
	using Reports;

	public sealed class CrashAnalyzer
	{
		private readonly MinidumpReader _reader = new();
		private readonly CrashReportBuilder _reportBuilder = new();

		public CrashAnalysisResult Analyze(String dumpPath, String? knownErrorsPath, String? contextPath = null)
		{
			var analysis = _reader.Read(dumpPath);
			var database = KnownErrorDatabase.Load(knownErrorsPath);
			var match = database.Match(analysis);
			var report = _reportBuilder.Build(analysis, match, contextPath);

			return new CrashAnalysisResult(analysis, match, report);
		}
	}
}