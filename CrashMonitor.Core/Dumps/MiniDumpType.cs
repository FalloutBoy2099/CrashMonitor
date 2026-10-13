namespace CrashMonitor.Core.Dumps
{
	using System;

	[Flags]
	internal enum MiniDumpType : UInt32
	{
		MiniDumpNormal = 0x00000000,
		MiniDumpWithDataSegs = 0x00000001,
		MiniDumpWithFullMemory = 0x00000002,
		MiniDumpWithHandleData = 0x00000004,
		MiniDumpWithThreadInfo = 0x00001000
	}
}