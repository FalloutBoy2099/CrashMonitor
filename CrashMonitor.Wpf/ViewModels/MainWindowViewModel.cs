namespace CrashMonitor.Wpf.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using CrashMonitor.Wpf.Models;
	using CrashMonitor.Wpf.Services;

	public partial class MainWindowViewModel : ObservableObject
	{
		private readonly ICrashMonitorUiService _crashMonitorService;
		private readonly IFileDialogService _fileDialogService;
		private readonly IMessageService _messageService;

		[ObservableProperty]
		private String dumpPath = String.Empty;

		[ObservableProperty]
		private String knownErrorsPath = Path.Combine(AppContext.BaseDirectory, "known-errors.json");

		[ObservableProperty]
		private String contextPath = String.Empty;

		[ObservableProperty]
		private String scriptPath = String.Empty;

		[ObservableProperty]
		private String unsafeAddress = String.Empty;

		[ObservableProperty]
		private String debugOutputPath = Path.Combine(Environment.CurrentDirectory, "sfall-debug-snapshot.md");

		[ObservableProperty]
		private String arrayIndexes = String.Empty;

		[ObservableProperty]
		private String localScriptIds = String.Empty;

		[ObservableProperty]
		private String critterIndexes = String.Empty;

		[ObservableProperty]
		private String maxRows = "100";

		[ObservableProperty]
		private String report = String.Empty;

		[ObservableProperty]
		private String status = "Ready";

		public MainWindowViewModel(
			ICrashMonitorUiService crashMonitorService,
			IFileDialogService fileDialogService,
			IMessageService messageService)
		{
			_crashMonitorService = crashMonitorService;
			_fileDialogService = fileDialogService;
			_messageService = messageService;
		}

		[RelayCommand]
		private void BrowseDump()
		{
			SetFromOpenDialog(value => DumpPath = value, DumpPath, "Dump files (*.dmp)|*.dmp|All files (*.*)|*.*");
		}

		[RelayCommand]
		private void BrowseKnownErrors()
		{
			SetFromOpenDialog(value => KnownErrorsPath = value, KnownErrorsPath, "JSON files (*.json)|*.json|All files (*.*)|*.*");
		}

		[RelayCommand]
		private void BrowseContext()
		{
			SetFromOpenDialog(value => ContextPath = value, ContextPath, "Markdown files (*.md)|*.md|All files (*.*)|*.*");
		}

		[RelayCommand]
		private void BrowseScript()
		{
			SetFromOpenDialog(value => ScriptPath = value, ScriptPath, "INT scripts (*.int)|*.int|All files (*.*)|*.*");
		}

		[RelayCommand]
		private void BrowseDebugOutput()
		{
			var selectedPath = _fileDialogService.SaveFile(DebugOutputPath, "Markdown files (*.md)|*.md|All files (*.*)|*.*");
			if (selectedPath is not null)
				DebugOutputPath = selectedPath;
		}

		[RelayCommand]
		private void AnalyzeDump()
		{
			RunWithStatus(() =>
			{
				Report = _crashMonitorService.AnalyzeDump(new DumpAnalysisRequest(
					DumpPath,
					EmptyToNull(KnownErrorsPath),
					EmptyToNull(ContextPath)));
			});
		}

		[RelayCommand]
		private void AnalyzeScript()
		{
			RunWithStatus(() =>
			{
				Report = _crashMonitorService.AnalyzeScript(new ScriptAnalysisRequest(
					ScriptPath,
					String.IsNullOrWhiteSpace(UnsafeAddress) ? null : ParseAddress(UnsafeAddress)));
			});
		}

		[RelayCommand]
		private async Task CaptureSfall()
		{
			await RunWithStatusAsync(async () =>
			{
				Report = await _crashMonitorService.CaptureSfallDebugAsync(new SfallDebugCaptureRequest(
					EmptyToNull(DebugOutputPath),
					ParseIndexList(ArrayIndexes),
					ParseIndexList(LocalScriptIds),
					ParseIndexList(CritterIndexes),
					ParseMaxRows()));
			});
		}

		private void SetFromOpenDialog(Action<String> setPath, String currentPath, String filter)
		{
			String? selectedPath = _fileDialogService.OpenFile(currentPath, filter);
			if (selectedPath is not null)
				setPath(selectedPath);
		}

		private void RunWithStatus(Action action)
		{
			try
			{
				Status = "Working...";
				action();
				Status = "Done";
			}
			catch (Exception exception)
			{
				Status = "Error";
				_messageService.ShowError(exception.Message);
			}
		}

		private async Task RunWithStatusAsync(Func<Task> action)
		{
			try
			{
				Status = "Working...";
				await action();
				Status = "Done";
			}
			catch (Exception exception)
			{
				Status = "Error";
				_messageService.ShowError(exception.Message);
			}
		}

		private Int32 ParseMaxRows()
		{
			if (Int32.TryParse(MaxRows, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 value))
				return Math.Max(1, value);

			return 100;
		}

		private static String? EmptyToNull(String value)
		{
			return String.IsNullOrWhiteSpace(value) ? null : value;
		}

		private static UInt32 ParseAddress(String value)
		{
			String clean = value.Trim();
			if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				clean = clean[2..];

			return UInt32.Parse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		}

		private static IReadOnlyList<Int32> ParseIndexList(String value)
		{
			if (String.IsNullOrWhiteSpace(value))
				return [];

			List<Int32> values = [];
			foreach (String item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				if (Int32.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 parsed))
					values.Add(parsed);

			return values;
		}
	}
}