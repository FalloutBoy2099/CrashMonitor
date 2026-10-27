namespace CrashMonitor.Wpf
{
	using System.Windows;
	using Services;
	using ViewModels;

	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			DataContext = new MainWindowViewModel(
				new CrashMonitorUiService(),
				new FileDialogService(),
				new MessageService());
		}
	}
}