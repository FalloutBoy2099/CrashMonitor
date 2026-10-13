namespace CrashMonitor.Wpf.Models
{
	using System;
	using System.Collections.Generic;

	public sealed record DumpAnalysisRequest(String DumpPath, String? KnownErrorsPath, String? ContextPath);

	public sealed record ScriptAnalysisRequest(String ScriptPath, UInt32? UnsafeAddress);

	public sealed record SfallDebugCaptureRequest(
		String? OutputPath,
		IReadOnlyList<Int32> ArrayIndexes,
		IReadOnlyList<Int32> LocalScriptIds,
		IReadOnlyList<Int32> CritterIndexes,
		Int32 MaxRows);
}
