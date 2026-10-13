namespace CrashMonitor.Core.Monitoring
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;

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