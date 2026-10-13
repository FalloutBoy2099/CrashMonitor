namespace CrashMonitor.Core.Monitoring
{
	using System;
	using System.Diagnostics;

	internal static class ProcessExtensions
	{
		public static DateTime? StartTimeSafe(this Process process)
		{
			try { return process.StartTime; }
			catch { return null; }
		}

		public static String? MainModuleFileNameSafe(this Process process)
		{
			try { return process.MainModule?.FileName; }
			catch { return null; }
		}

		public static Int32? ExitCodeSafe(this Process process)
		{
			try { return process.ExitCode; }
			catch { return null; }
		}
	}
}