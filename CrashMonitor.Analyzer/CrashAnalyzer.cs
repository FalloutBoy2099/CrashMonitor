namespace CrashMonitor.Analyzer
{
	using CrashMonitor.Analyzer.KnownErrors;
	using CrashMonitor.Analyzer.Minidumps;
	using CrashMonitor.Analyzer.Reports;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;

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