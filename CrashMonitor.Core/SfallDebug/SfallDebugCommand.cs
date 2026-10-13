namespace CrashMonitor.Core.SfallDebug
{
	using System;

	internal enum SfallDebugCommand : Byte
	{
		SetGlobal = 0,
		SetMapVariable = 1,
		GetCritter = 2,
		SetCritter = 3,
		SetSfallGlobal = 4,
		GetArray = 9,
		SetArray = 10,
		GetLocalVariables = 11,
		SetLocalVariables = 12,
		Exit = 254
	}
}