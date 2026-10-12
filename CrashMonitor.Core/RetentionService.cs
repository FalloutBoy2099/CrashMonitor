namespace CrashMonitor.Core
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;
	public sealed class RetentionService
	{
		public void Apply(String outputDirectory, Int32 keepLastCaptures)
		{
			if (keepLastCaptures <= 0 || !Directory.Exists(outputDirectory))
			{
				return;
			}

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