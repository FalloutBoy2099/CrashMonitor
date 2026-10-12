namespace CrashMonitor.Core.Configuration
{
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

	public sealed class CrashMonitorConfig
	{
		public String ProcessName { get; set; } = "fallout2";
		public String? FalloutDirectory { get; set; }
		public String OutputDirectory { get; set; } = "CrashReports";
		public DumpKind DumpKind { get; set; } = DumpKind.Normal;
		public Int32 PollIntervalMilliseconds { get; set; } = 1000;
		public Boolean DetectNotResponding { get; set; } = true;
		public Int32 NotRespondingSeconds { get; set; } = 15;
		public Int32 KeepLastDumps { get; set; } = 20;
		public Boolean CopyRecentSaves { get; set; }
		public Int32 RecentSaveCount { get; set; } = 1;
		public Boolean CopyLogs { get; set; } = true;
		public String KnownErrorsPath { get; set; } = "known-errors.json";
	}
}