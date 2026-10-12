namespace CrashMonitor.Analyzer.Minidumps
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
	public sealed record MinidumpAnalysis(
		String DumpPath,
		ExceptionRecord? Exception,
		IReadOnlyList<ModuleRecord> Modules,
		IReadOnlyList<ThreadRecord> Threads)
	{
		public ModuleRecord? FaultingModule =>
			Exception is null ? null : Modules.FirstOrDefault(module => module.Contains(Exception.Address));
	}

	public sealed record ExceptionRecord(
		Int32 ThreadId,
		UInt32 Code,
		UInt64 Address,
		IReadOnlyList<UInt64> Parameters,
		UInt32 ContextRva,
		UInt32 ContextSize,
		IReadOnlyDictionary<String, UInt32> Registers);

	public sealed record ModuleRecord(UInt64 BaseAddress, UInt64 EndAddress, UInt32 Size, String Path)
	{
		public String Name => System.IO.Path.GetFileName(Path);

		public Boolean Contains(UInt64 address)
		{
			return address >= BaseAddress && address < EndAddress;
		}

		public UInt64 OffsetOf(UInt64 address)
		{
			return address - BaseAddress;
		}
	}

	public sealed record ThreadRecord(
		Int32 ThreadId,
		UInt64 StackStart,
		UInt32 StackSize,
		UInt32 StackRva,
		IReadOnlyDictionary<String, UInt32> Registers);
}