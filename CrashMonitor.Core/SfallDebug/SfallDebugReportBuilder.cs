namespace CrashMonitor.Core.SfallDebug
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;

	public sealed class SfallDebugReportBuilder
	{
		public String Build(
			SfallDebugSnapshot snapshot,
			IReadOnlyDictionary<Int32, IReadOnlyList<SfallDebugArrayItem>> arrayValues,
			IReadOnlyDictionary<Int32, SfallDebugLocalVariables> localVariables,
			Int32 maxRows)
		{
			var builder = new StringBuilder();
			builder.AppendLine("# sfall Debug Snapshot");
			builder.AppendLine();
			builder.AppendLine($"- Captured: `{DateTimeOffset.Now:O}`");
			builder.AppendLine($"- Globals: `{snapshot.Globals.Count}`");
			builder.AppendLine($"- Map variables: `{snapshot.MapVariables.Count}`");
			builder.AppendLine($"- sfall globals: `{snapshot.SfallGlobals.Count}`");
			builder.AppendLine($"- sfall arrays: `{snapshot.Arrays.Count}`");
			builder.AppendLine($"- Critters: `{snapshot.Critters.Count}`");

			AppendIntTable(builder, "Game Globals", snapshot.Globals, maxRows);
			AppendIntTable(builder, "Map Variables", snapshot.MapVariables, maxRows);
			AppendSfallGlobals(builder, snapshot.SfallGlobals, maxRows);
			AppendArrays(builder, snapshot.Arrays, arrayValues, maxRows);
			AppendCritters(builder, snapshot.Critters, localVariables, maxRows);

			return builder.ToString();
		}

		private static void AppendIntTable(StringBuilder builder, String title, IReadOnlyList<Int32> values, Int32 maxRows)
		{
			builder.AppendLine();
			builder.AppendLine($"## {title}");
			builder.AppendLine();
			builder.AppendLine("| Index | Value | Hex |");
			builder.AppendLine("| ---: | ---: | --- |");

			foreach (var item in values.Select((value, index) => new { value, index }).Take(maxRows))
				builder.AppendLine($"| {item.index.ToString(CultureInfo.InvariantCulture)} | {item.value.ToString(CultureInfo.InvariantCulture)} | `0x{item.value:X8}` |");

			AppendTruncated(builder, values.Count, maxRows);
		}

		private static void AppendSfallGlobals(StringBuilder builder, IReadOnlyList<SfallDebugGlobal> globals, Int32 maxRows)
		{
			builder.AppendLine();
			builder.AppendLine("## sfall Globals");
			builder.AppendLine();
			builder.AppendLine("| Index | Key | Value | Hex |");
			builder.AppendLine("| ---: | --- | ---: | --- |");

			foreach (var item in globals.Select((value, index) => new { value, index }).Take(maxRows))
				builder.AppendLine($"| {item.index.ToString(CultureInfo.InvariantCulture)} | `{item.value.Name}` | {item.value.Value.ToString(CultureInfo.InvariantCulture)} | `0x{item.value.Value:X8}` |");

			AppendTruncated(builder, globals.Count, maxRows);
		}

		private static void AppendArrays(
			StringBuilder builder,
			IReadOnlyList<SfallDebugArrayHeader> arrays,
			IReadOnlyDictionary<Int32, IReadOnlyList<SfallDebugArrayItem>> arrayValues,
			Int32 maxRows)
		{
			builder.AppendLine();
			builder.AppendLine("## sfall Arrays");
			builder.AppendLine();
			builder.AppendLine("| Index | Id | Scope | Length | Flags |");
			builder.AppendLine("| ---: | ---: | --- | ---: | --- |");

			foreach (var item in arrays.Select((value, index) => new { value, index }).Take(maxRows))
				builder.AppendLine($"| {item.index.ToString(CultureInfo.InvariantCulture)} | {item.value.Id.ToString(CultureInfo.InvariantCulture)} | {(item.value.IsMap ? "map" : "global")} | {item.value.Length.ToString(CultureInfo.InvariantCulture)} | `0x{item.value.Flags:X8}` |");

			AppendTruncated(builder, arrays.Count, maxRows);

			foreach (var pair in arrayValues.OrderBy(item => item.Key))
			{
				builder.AppendLine();
				builder.AppendLine($"### Array {pair.Key.ToString(CultureInfo.InvariantCulture)} Values");
				builder.AppendLine();
				builder.AppendLine("| Index | Type | Value | Raw length |");
				builder.AppendLine("| ---: | --- | --- | ---: |");

				foreach (var value in pair.Value.Take(maxRows))
					builder.AppendLine($"| {value.Index.ToString(CultureInfo.InvariantCulture)} | `{value.Type}` | `{EscapePipe(value.Value)}` | {value.RawLength.ToString(CultureInfo.InvariantCulture)} |");

				AppendTruncated(builder, pair.Value.Count, maxRows);
			}
		}

		private static void AppendCritters(
			StringBuilder builder,
			IReadOnlyList<SfallDebugCritter> critters,
			IReadOnlyDictionary<Int32, SfallDebugLocalVariables> localVariables,
			Int32 maxRows)
		{
			builder.AppendLine();
			builder.AppendLine("## Critters");
			builder.AppendLine();
			builder.AppendLine("| Index | FID | Pointer | Script ID |");
			builder.AppendLine("| ---: | --- | --- | ---: |");

			foreach (var critter in critters.Take(maxRows))
			{
				var scriptId = critter.ScriptId?.ToString(CultureInfo.InvariantCulture) ?? "";
				builder.AppendLine($"| {critter.Index.ToString(CultureInfo.InvariantCulture)} | `0x{critter.Fid:X8}` | `0x{critter.Pointer:X8}` | {scriptId} |");
			}

			AppendTruncated(builder, critters.Count, maxRows);

			foreach (var pair in localVariables.OrderBy(item => item.Key))
			{
				builder.AppendLine();
				builder.AppendLine($"### Local Variables for Script {pair.Key.ToString(CultureInfo.InvariantCulture)}");
				builder.AppendLine();
				AppendIntTable(builder, "Values", pair.Value.Values, maxRows);
			}
		}

		private static void AppendTruncated(StringBuilder builder, Int32 total, Int32 maxRows)
		{
			if (total > maxRows)
				builder.AppendLine($"| ... | {total - maxRows} more row(s) omitted | |");
		}

		private static String EscapePipe(String value)
		{
			return value.Replace("|", "\\|", StringComparison.Ordinal);
		}
	}
}