namespace CrashMonitor.Wpf.Services
{
	using System;
	using System.Threading.Tasks;
	using CrashMonitor.Wpf.Models;

	public interface ICrashMonitorUiService
	{
		String AnalyzeDump(DumpAnalysisRequest request);

		String AnalyzeScript(ScriptAnalysisRequest request);

		Task<String> CaptureSfallDebugAsync(SfallDebugCaptureRequest request);
	}
}
