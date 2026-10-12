namespace CrashMonitor.Analyzer
{
	using CrashMonitor.Analyzer.KnownErrors;
	using CrashMonitor.Analyzer.Minidumps;
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

	public sealed record CrashAnalysisResult(MinidumpAnalysis Dump, KnownErrorMatch? KnownError, String Markdown);
}