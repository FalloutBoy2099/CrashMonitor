namespace CrashMonitor.Wpf.Services
{
	using System;
	using System.Windows;

	public sealed class MessageService : IMessageService
	{
		public void ShowError(String message)
		{
			MessageBox.Show(message, "CrashMonitor", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}
}
