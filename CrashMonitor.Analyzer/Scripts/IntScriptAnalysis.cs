namespace CrashMonitor.Analyzer.Scripts
{
	using System;
	using System.Collections.Generic;

	public sealed record IntScriptAnalysis(
		String Path,
		IReadOnlyList<IntScriptProcedure> Procedures,
		IReadOnlyList<IntScriptUnsafeCall> UnsafeCalls,
		IReadOnlyList<String> StaticStrings);

	public sealed record IntScriptProcedure(
		Int32 Index,
		String Name,
		UInt32 BodyOffset,
		UInt32 ConditionOffset,
		UInt32 Flags,
		Int32 ArgumentCount);

	public sealed record IntScriptInstruction(
		UInt32 Offset,
		UInt32 Opcode,
		String Mnemonic,
		UInt32? Operand,
		String? OperandText);

	public sealed record IntScriptUnsafeCall(
		IntScriptProcedure Procedure,
		IntScriptInstruction Instruction,
		IReadOnlyList<IntScriptInstruction> Context,
		UInt32? DirectAddress,
		UInt32? FirstArgument);
}