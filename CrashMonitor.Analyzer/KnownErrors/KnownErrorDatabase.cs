namespace CrashMonitor.Analyzer.KnownErrors
{
	using System.Globalization;
	using System.Text.Json;
	using CrashMonitor.Analyzer.Minidumps;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	public sealed class KnownErrorDatabase
	{
		private readonly IReadOnlyList<KnownErrorEntry> _entries;

		private KnownErrorDatabase(IReadOnlyList<KnownErrorEntry> entries)
		{
			_entries = entries;
		}

		public static KnownErrorDatabase Load(String? path)
		{
			if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				return new KnownErrorDatabase([]);
			}

			var json = File.ReadAllText(path);
			var entries = JsonSerializer.Deserialize<List<KnownErrorEntry>>(json, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			}) ?? [];
			return new KnownErrorDatabase(entries);
		}

		public KnownErrorMatch? Match(MinidumpAnalysis analysis)
		{
			if (analysis.Exception is null)
			{
				return null;
			}

			var module = analysis.FaultingModule;
			var address = analysis.Exception.Address;
			var relativeAddress = module is null ? address : address - module.BaseAddress;
			KnownErrorMatch? best = null;

			foreach (var entry in _entries)
			{
				if (!String.IsNullOrWhiteSpace(entry.Module) && module is not null &&
					!String.Equals(entry.Module, module.Name, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var distance = MatchAddress(entry, address, relativeAddress);
				if (distance is null)
				{
					continue;
				}

				if (best is null || distance.Value < best.Distance)
				{
					best = new KnownErrorMatch(entry, distance.Value);
				}
			}

			return best;
		}

		private static Int64? MatchAddress(KnownErrorEntry entry, UInt64 absoluteAddress, UInt64 relativeAddress)
		{
			if (!String.IsNullOrWhiteSpace(entry.Address))
			{
				var target = ParseHex(entry.Address);
				var distance = Distance(absoluteAddress, target);
				var relativeDistance = Distance(relativeAddress, target);
				var best = Math.Min(distance, relativeDistance);
				return best <= entry.Tolerance ? best : null;
			}

			if (!String.IsNullOrWhiteSpace(entry.Start) && !String.IsNullOrWhiteSpace(entry.End))
			{
				var start = ParseHex(entry.Start);
				var end = ParseHex(entry.End);
				if ((absoluteAddress >= start && absoluteAddress <= end) || (relativeAddress >= start && relativeAddress <= end))
				{
					return 0;
				}
			}

			return null;
		}

		private static Int64 Distance(UInt64 left, UInt64 right)
		{
			return left > right ? checked((Int64)(left - right)) : checked((Int64)(right - left));
		}

		private static UInt64 ParseHex(String text)
		{
			var clean = text.Trim();
			if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				clean = clean[2..];
			}

			return UInt64.Parse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		}
	}
}