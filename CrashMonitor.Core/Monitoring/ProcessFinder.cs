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

	public sealed class ProcessFinder
	{
		public Process? FindFirst(String processName)
		{
			var normalized = Path.GetFileNameWithoutExtension(processName);
			return Process.GetProcessesByName(normalized)
				.OrderBy(process => process.StartTimeSafe() ?? DateTime.MaxValue)
				.FirstOrDefault();
		}

		public Process? FindById(Int32 processId)
		{
			try { return Process.GetProcessById(processId); }
			catch (ArgumentException) { return null; }
		}
	}
}