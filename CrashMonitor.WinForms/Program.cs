namespace CrashMonitor.WinForms
{
	using System;
	using System.Windows.Forms;

	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			ApplicationConfiguration.Initialize();
			Application.Run(new Form1());
		}
	}
}