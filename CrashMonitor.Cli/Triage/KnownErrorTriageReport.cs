namespace CrashMonitor.Cli.Triage
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using CrashMonitor.Analyzer.KnownErrors;

	internal sealed class KnownErrorTriageReport
	{
		public String BuildMarkdown(IReadOnlyList<KnownErrorEntry> entries, Int32 limit)
		{
			var groups = Group(entries)
				.Take(limit)
				.ToArray();
			var builder = new StringBuilder();
			builder.AppendLine("# CrashMonitor Triage");
			builder.AppendLine();
			builder.AppendLine($"- Entries: `{entries.Count}`");
			builder.AppendLine($"- Address groups: `{Group(entries).Count}`");
			builder.AppendLine();
			builder.AppendLine("| Address | Count | Module | Status | Memory refs | Issues |");
			builder.AppendLine("| --- | ---: | --- | --- | --- | --- |");

			foreach (var group in groups)
			{
				builder.AppendLine($"| `{group.Address}` | {group.Count} | {Escape(group.Module)} | {Escape(group.Status)} | {Escape(group.MemoryReferences)} | {Escape(group.Issues)} |");
			}

			return builder.ToString();
		}

		public IReadOnlyList<KnownErrorTriageGroup> Group(IReadOnlyList<KnownErrorEntry> entries)
		{
			return entries
				.Where(entry => !String.IsNullOrWhiteSpace(entry.Address))
				.GroupBy(entry => NormalizeAddress(entry.Address!), StringComparer.OrdinalIgnoreCase)
				.Select(group => new KnownErrorTriageGroup(
					group.Key,
					group.Count(),
					MostCommon(group.Select(entry => entry.Module)),
					MostCommon(group.Select(entry => entry.Status)) ?? "Unknown",
					String.Join(", ", group.Select(entry => entry.ObservedMemoryAddress).Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5)),
					String.Join(", ", group.Select(entry => entry.Reference).Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5))))
				.OrderByDescending(group => group.Count)
				.ThenBy(group => group.Address, StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private static String? MostCommon(IEnumerable<String?> values)
		{
			return values
				.Where(value => !String.IsNullOrWhiteSpace(value))
				.GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
				.OrderByDescending(group => group.Count())
				.ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault()
				?.Key;
		}

		private static String NormalizeAddress(String address)
		{
			var clean = address.Trim();
			return clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
				? $"0x{clean[2..].PadLeft(8, '0').ToUpperInvariant()}"
				: $"0x{clean.PadLeft(8, '0').ToUpperInvariant()}";
		}

		private static String Escape(String? text)
		{
			if (String.IsNullOrWhiteSpace(text))
			{
				return "";
			}

			return text.Replace("|", "\\|", StringComparison.Ordinal);
		}
	}
}