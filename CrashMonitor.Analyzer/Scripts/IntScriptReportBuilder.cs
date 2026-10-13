namespace CrashMonitor.Analyzer.Scripts
{
	using System;
	using System.Linq;
	using System.Text;

	public sealed class IntScriptReportBuilder
	{
		public String Build(IntScriptAnalysis analysis, UInt32? unsafeAddress = null)
		{
			var builder = new StringBuilder();
			builder.AppendLine("# INT Script Analysis");
			builder.AppendLine();
			builder.AppendLine($"- Script: `{analysis.Path}`");
			builder.AppendLine($"- Procedures: `{analysis.Procedures.Count}`");
			builder.AppendLine($"- Unsafe calls: `{analysis.UnsafeCalls.Count}`");

			if (unsafeAddress is not null)
				builder.AppendLine($"- Focus address: `{FormatHex(unsafeAddress.Value)}`");

			builder.AppendLine();
			AppendUnsafeCalls(builder, analysis, unsafeAddress);
			AppendProcedures(builder, analysis);

			return builder.ToString();
		}

		private static void AppendUnsafeCalls(StringBuilder builder, IntScriptAnalysis analysis, UInt32? unsafeAddress)
		{
			builder.AppendLine("## Unsafe Calls");

			var calls = analysis.UnsafeCalls
				.Where(call => unsafeAddress is null || call.DirectAddress == unsafeAddress)
				.ToArray();

			if (calls.Length == 0)
			{
				builder.AppendLine("No matching unsafe calls.");
				builder.AppendLine();
				return;
			}

			foreach (var call in calls)
			{
				builder.AppendLine($"### {call.Procedure.Name} at `{FormatHex(call.Instruction.Offset)}`");
				builder.AppendLine($"- Opcode: `{call.Instruction.Mnemonic}`");

				if (call.DirectAddress is not null)
					builder.AppendLine($"- Direct address: `{FormatHex(call.DirectAddress.Value)}`");

				if (call.FirstArgument is not null)
					builder.AppendLine($"- First argument: `{FormatHex(call.FirstArgument.Value)}`");

				builder.AppendLine();
				builder.AppendLine("```text");

				foreach (var instruction in call.Context)
					builder.AppendLine($"{FormatHex(instruction.Offset)}  {FormatInstruction(instruction)}");

				builder.AppendLine("```");
				builder.AppendLine();
			}
		}

		private static void AppendProcedures(StringBuilder builder, IntScriptAnalysis analysis)
		{
			builder.AppendLine("## Procedures");
			foreach (var procedure in analysis.Procedures)
				builder.AppendLine($"- `{procedure.Index:D2}` `{procedure.Name}` body `{FormatHex(procedure.BodyOffset)}` args `{procedure.ArgumentCount}`");
		}

		private static String FormatInstruction(IntScriptInstruction instruction)
		{
			if (instruction.Operand is null)
				return instruction.Mnemonic;

			return String.IsNullOrWhiteSpace(instruction.OperandText)
				? $"{instruction.Mnemonic} {FormatHex(instruction.Operand.Value)}"
				: $"{instruction.Mnemonic} {instruction.OperandText}";
		}

		private static String FormatHex(UInt32 value)
		{
			return $"0x{value:X8}";
		}
	}
}