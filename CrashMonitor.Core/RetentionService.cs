namespace CrashMonitor.Core
{
	using System;
	using System.IO;
	using System.Linq;

	public sealed class RetentionService
	{
		public void Apply(String outputDirectory, Int32 keepLastCaptures)
		{
			if (keepLastCaptures <= 0 || !Directory.Exists(outputDirectory))
				return;

			var oldDirectories = Directory.EnumerateDirectories(outputDirectory)
				.Select(path => new DirectoryInfo(path))
				.OrderByDescending(info => info.CreationTimeUtc)
				.Skip(keepLastCaptures);

			foreach (var directory in oldDirectories)
			{
				try { directory.Delete(recursive: true); }
				catch { }
			}
		}
	}
}