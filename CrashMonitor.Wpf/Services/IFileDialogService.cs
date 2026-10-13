namespace CrashMonitor.Wpf.Services
{
	using System;

	public interface IFileDialogService
	{
		String? OpenFile(String currentPath, String filter);

		String? SaveFile(String currentPath, String filter);
	}
}
