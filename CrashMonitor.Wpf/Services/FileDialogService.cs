namespace CrashMonitor.Wpf.Services
{
	using System;
	using Microsoft.Win32;

	public sealed class FileDialogService : IFileDialogService
	{
		public String? OpenFile(String currentPath, String filter)
		{
			OpenFileDialog dialog = new()
			{
				Filter = filter,
				FileName = currentPath
			};

			return dialog.ShowDialog() == true ? dialog.FileName : null;
		}

		public String? SaveFile(String currentPath, String filter)
		{
			SaveFileDialog dialog = new()
			{
				Filter = filter,
				FileName = currentPath
			};

			return dialog.ShowDialog() == true ? dialog.FileName : null;
		}
	}
}
