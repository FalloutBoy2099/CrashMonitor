namespace CrashMonitor.Core.SfallDebug
{
	using System;
	using System.Collections.Generic;

	public sealed record SfallDebugSnapshot(
		IReadOnlyList<Int32> Globals,
		IReadOnlyList<Int32> MapVariables,
		IReadOnlyList<SfallDebugGlobal> SfallGlobals,
		IReadOnlyList<SfallDebugArrayHeader> Arrays,
		IReadOnlyList<SfallDebugCritter> Critters);

	public sealed record SfallDebugGlobal(UInt64 Key, String Name, Int32 Value);

	public sealed record SfallDebugArrayHeader(UInt32 Id, Boolean IsMap, Int32 Length, Int32 Flags);

	public sealed record SfallDebugArrayItem(Int32 Index, SfallDebugArrayValueType Type, Int32 RawLength, String Value);

	public sealed record SfallDebugCritter(Int32 Index, UInt32 Fid, UInt32 Pointer, Int32? ScriptId);

	public sealed record SfallDebugCritterDetails(Int32 Index, IReadOnlyList<Int32> RawFields);

	public sealed record SfallDebugLocalVariables(Int32 ScriptId, IReadOnlyList<Int32> Values);
}