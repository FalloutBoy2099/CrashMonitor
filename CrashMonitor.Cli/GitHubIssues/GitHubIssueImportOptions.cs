namespace CrashMonitor.Cli.GitHubIssues
{
	using System;

	internal sealed class GitHubIssueImportOptions
	{
		public String Repository { get; set; } = "egornovivan/CrashMonitor";
		public String State { get; set; } = "all";
		public Int32 MaxPages { get; set; } = 5;
		public UInt32 MinAddress { get; set; } = 0x00400000;
		public Boolean IncludeLowAddresses { get; set; }
		public Boolean OnlyFalloutExe { get; set; }
	}
}