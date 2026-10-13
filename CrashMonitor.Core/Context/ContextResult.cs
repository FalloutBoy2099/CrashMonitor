namespace CrashMonitor.Core.Context
{
	using System;
	using System.Collections.Generic;

	public sealed record ContextResult(String CaptureDirectory, String ContextPath, IReadOnlyList<String> CopiedFiles);
}