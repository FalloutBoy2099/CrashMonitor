namespace CrashMonitor.Wpf.Services
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using CrashMonitor.Analyzer;
	using CrashMonitor.Analyzer.Scripts;
	using CrashMonitor.Core.SfallDebug;
	using CrashMonitor.Wpf.Models;

	public sealed class CrashMonitorUiService : ICrashMonitorUiService
	{
		private readonly CrashAnalyzer _crashAnalyzer = new();
		private readonly IntScriptAnalyzer _scriptAnalyzer = new();
		private readonly IntScriptReportBuilder _scriptReportBuilder = new();
		private readonly SfallDebugReportBuilder _sfallReportBuilder = new();

		public String AnalyzeDump(DumpAnalysisRequest request)
		{
			return _crashAnalyzer.Analyze(request.DumpPath, request.KnownErrorsPath, request.ContextPath).Markdown;
		}

		public String AnalyzeScript(ScriptAnalysisRequest request)
		{
			IntScriptAnalysis analysis = _scriptAnalyzer.Analyze(request.ScriptPath);
			return _scriptReportBuilder.Build(analysis, request.UnsafeAddress);
		}

		public Task<String> CaptureSfallDebugAsync(SfallDebugCaptureRequest request)
		{
			return Task.Run(() => CaptureSfallDebug(request));
		}

		private String CaptureSfallDebug(SfallDebugCaptureRequest request)
		{
			using SfallDebugClient client = SfallDebugClient.Connect();
			SfallDebugSnapshot snapshot = client.ReadSnapshot();
			Dictionary<Int32, IReadOnlyList<SfallDebugArrayItem>> arrays = new();
			foreach (Int32 index in request.ArrayIndexes)
			{
				if (index >= 0 && index < snapshot.Arrays.Count)
				{
					arrays[index] = client.GetArrayValues(index, snapshot.Arrays[index]);
				}
			}

			Dictionary<Int32, SfallDebugLocalVariables> locals = new();
			foreach (Int32 scriptId in request.LocalScriptIds)
			{
				locals[scriptId] = client.GetLocalVariables(scriptId);
			}

			foreach (Int32 critterIndex in request.CritterIndexes)
			{
				if (critterIndex >= 0 && critterIndex < snapshot.Critters.Count)
				{
					SfallDebugCritterDetails details = client.GetCritterDetails(critterIndex);
					if (details.RawFields.Count > 0 && details.RawFields[0] > 0)
					{
						locals[details.RawFields[0]] = client.GetLocalVariables(details.RawFields[0]);
					}
				}
			}

			String report = _sfallReportBuilder.Build(snapshot, arrays, locals, request.MaxRows);
			client.Exit();
			if (!String.IsNullOrWhiteSpace(request.OutputPath))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputPath))!);
				File.WriteAllText(request.OutputPath, report);
			}

			return report;
		}
	}
}
