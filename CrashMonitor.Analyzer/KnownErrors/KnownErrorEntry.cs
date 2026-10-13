namespace CrashMonitor.Analyzer.KnownErrors
{
	using System;

	public sealed class KnownErrorEntry
	{
		public String Id { get; set; } = String.Empty;
		public String? Module { get; set; }
		public String? Address { get; set; }
		public String? Start { get; set; }
		public String? End { get; set; }
		public Int32 Tolerance { get; set; }
		public String Description { get; set; } = String.Empty;
		public String Cause { get; set; } = String.Empty;
		public String Workaround { get; set; } = String.Empty;
		public String? Reference { get; set; }
		public String? Status { get; set; }
		public String? Source { get; set; }
		public String? ObservedMemoryAddress { get; set; }
	}
}