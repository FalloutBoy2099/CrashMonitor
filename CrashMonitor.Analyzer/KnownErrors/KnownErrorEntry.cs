namespace CrashMonitor.Analyzer.KnownErrors
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
	public sealed class KnownErrorEntry
	{
		public String Id { get; set; } = "";
		public String? Module { get; set; }
		public String? Address { get; set; }
		public String? Start { get; set; }
		public String? End { get; set; }
		public Int32 Tolerance { get; set; }
		public String Description { get; set; } = "";
		public String Cause { get; set; } = "";
		public String Workaround { get; set; } = "";
		public String? Reference { get; set; }
	}
}