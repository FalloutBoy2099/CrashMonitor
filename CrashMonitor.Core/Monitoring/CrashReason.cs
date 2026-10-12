namespace CrashMonitor.Core.Monitoring
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
	public enum CrashReason
	{
		ManualDump,
		ProcessExitedUnexpectedly,
		NotResponding
	}
}