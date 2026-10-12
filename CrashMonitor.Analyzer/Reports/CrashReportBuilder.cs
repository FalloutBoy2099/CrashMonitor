namespace CrashMonitor.Analyzer.Reports
{
	using System.Text;
	using CrashMonitor.Analyzer.KnownErrors;
	using CrashMonitor.Analyzer.Minidumps;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;

	public sealed class CrashReportBuilder
	{
		public String Build(MinidumpAnalysis analysis, KnownErrorMatch? match, String? contextPath = null)
		{
			var builder = new StringBuilder();
			builder.AppendLine("# CrashMonitor Report");
			builder.AppendLine();
			builder.AppendLine($"- Dump: `{analysis.DumpPath}`");
			builder.AppendLine($"- Generated: `{DateTimeOffset.Now:O}`");

			if (contextPath is not null)
			{
				builder.AppendLine($"- Context: `{contextPath}`");
			}

			builder.AppendLine();
			AppendSummary(builder, analysis, match);
			AppendException(builder, analysis);
			AppendRegisters(builder, analysis);
			AppendModules(builder, analysis);
			return builder.ToString();
		}

		private static void AppendSummary(StringBuilder builder, MinidumpAnalysis analysis, KnownErrorMatch? match)
		{
			builder.AppendLine("## Summary");
			if (analysis.Exception is null)
			{
				builder.AppendLine("No exception stream was found in this minidump.");
				builder.AppendLine();
				return;
			}

			var module = analysis.FaultingModule;
			var exception = analysis.Exception;
			builder.AppendLine($"- Exception: `{FormatHex(exception.Code)}` {ExceptionName(exception.Code)}");
			builder.AppendLine($"- Address: `{FormatHex(exception.Address)}`");
			if (module is not null)
			{
				builder.AppendLine($"- Module: `{module.Name}+0x{module.OffsetOf(exception.Address):X}`");
			}

			if (exception.Code == 0xC0000005 && exception.Parameters.Count >= 2)
			{
				builder.AppendLine($"- Access violation: {AccessKind(exception.Parameters[0])} at `{FormatHex(exception.Parameters[1])}`");
			}

			if (match is null)
			{
				builder.AppendLine("- Known error: no local match");
			}
			else
			{
				builder.AppendLine($"- Known error: `{match.Entry.Id}` ({match.Entry.Description})");
				builder.AppendLine($"- Cause: {match.Entry.Cause}");
				builder.AppendLine($"- Workaround: {match.Entry.Workaround}");
				if (!String.IsNullOrWhiteSpace(match.Entry.Reference))
				{
					builder.AppendLine($"- Reference: {match.Entry.Reference}");
				}
			}

			builder.AppendLine();
		}

		private static void AppendException(StringBuilder builder, MinidumpAnalysis analysis)
		{
			builder.AppendLine("## Exception");
			if (analysis.Exception is null)
			{
				builder.AppendLine("No exception data.");
				builder.AppendLine();
				return;
			}

			var exception = analysis.Exception;
			builder.AppendLine($"- Thread: `{exception.ThreadId}`");
			builder.AppendLine($"- Code: `{FormatHex(exception.Code)}`");
			builder.AppendLine($"- Address: `{FormatHex(exception.Address)}`");
			builder.AppendLine($"- Parameters: `{String.Join(", ", exception.Parameters.Select(FormatHex))}`");
			builder.AppendLine();
		}

		private static void AppendRegisters(StringBuilder builder, MinidumpAnalysis analysis)
		{
			builder.AppendLine("## Registers");
			if (analysis.Exception is null || analysis.Exception.Registers.Count == 0)
			{
				builder.AppendLine("No register data.");
				builder.AppendLine();
				return;
			}

			builder.AppendLine(String.Join(" ", analysis.Exception.Registers.Select(item => $"{item.Key}={FormatHex(item.Value)}")));
			builder.AppendLine();
		}

		private static void AppendModules(StringBuilder builder, MinidumpAnalysis analysis)
		{
			builder.AppendLine("## Modules");
			foreach (var module in analysis.Modules.Take(80))
			{
				builder.AppendLine($"- `{FormatHex(module.BaseAddress)}-{FormatHex(module.EndAddress)}` `{module.Name}`");
			}

			if (analysis.Modules.Count > 80)
			{
				builder.AppendLine($"- ... {analysis.Modules.Count - 80} more modules");
			}
		}

		private static String ExceptionName(UInt32 code)
		{
			return code switch
			{
				0xC0000005 => "Access violation",
				0x80000003 => "Breakpoint",
				0xC000001D => "Illegal instruction",
				0xC0000094 => "Integer divide by zero",
				0xC00000FD => "Stack overflow",
				_ => "Unknown"
			};
		}

		private static String AccessKind(UInt64 kind)
		{
			return kind switch
			{
				0 => "read",
				1 => "write",
				8 => "execute",
				_ => $"operation {kind}"
			};
		}

		private static String FormatHex(UInt32 value)
		{
			return $"0x{value:X8}";
		}

		private static String FormatHex(UInt64 value)
		{
			return value <= UInt32.MaxValue ? $"0x{value:X8}" : $"0x{value:X16}";
		}
	}
}