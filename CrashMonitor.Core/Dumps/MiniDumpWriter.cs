namespace CrashMonitor.Core.Dumps
{
	using System;
	using System.IO;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.InteropServices;
	using Configuration;

	public sealed class MiniDumpWriter
	{
		public DumpResult Write(Process process, String outputPath, DumpKind kind)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

			using var file = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

			var dumpType = ToMiniDumpType(kind);

			var ok = MiniDumpWriteDump(
				process.Handle,
				process.Id,
				file.SafeFileHandle.DangerousGetHandle(),
				dumpType,
				IntPtr.Zero,
				IntPtr.Zero,
				IntPtr.Zero);

			if (!ok)
			{
				var error = Marshal.GetLastWin32Error();
				throw new Win32Exception(error, $"MiniDumpWriteDump failed for PID {process.Id}");
			}

			return new DumpResult(outputPath, dumpType.ToString(), new FileInfo(outputPath).Length);
		}

		private static MiniDumpType ToMiniDumpType(DumpKind kind)
		{
			return kind switch
			{
				DumpKind.Normal => MiniDumpType.MiniDumpNormal | MiniDumpType.MiniDumpWithThreadInfo,
				DumpKind.WithDataSegs => MiniDumpType.MiniDumpWithDataSegs | MiniDumpType.MiniDumpWithThreadInfo,
				DumpKind.FullMemory => MiniDumpType.MiniDumpWithFullMemory | MiniDumpType.MiniDumpWithHandleData | MiniDumpType.MiniDumpWithThreadInfo,
				_ => MiniDumpType.MiniDumpNormal
			};
		}

		[DllImport("dbghelp.dll", SetLastError = true)]
		private static extern Boolean MiniDumpWriteDump(
			IntPtr hProcess,
			Int32 processId,
			IntPtr hFile,
			MiniDumpType dumpType,
			IntPtr exceptionParam,
			IntPtr userStreamParam,
			IntPtr callbackParam);
	}
}