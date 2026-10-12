namespace CrashMonitor.Core.Monitoring
{
	using System.Diagnostics;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;

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