namespace CrashMonitor.Core.Configuration
{
	using System;
	using System.IO;
	using System.Text.Json;

	public static class ConfigStore
	{
		private static readonly JsonSerializerOptions Options = new()
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};

		public static CrashMonitorConfig LoadOrDefault(String? path)
		{
			if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
				return new CrashMonitorConfig();

			var json = File.ReadAllText(path);

			return JsonSerializer.Deserialize<CrashMonitorConfig>(json, Options) ?? new CrashMonitorConfig();
		}

		public static void Save(String path, CrashMonitorConfig config)
		{
			var directory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (!String.IsNullOrWhiteSpace(directory))
				Directory.CreateDirectory(directory);

			File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
		}
	}
}