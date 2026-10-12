namespace CrashMonitor.Analyzer.Minidumps
{
	using System.Buffers.Binary;
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

	public sealed class MinidumpReader
	{
		private const Int32 ThreadListStream = 3;
		private const Int32 ModuleListStream = 4;
		private const Int32 ExceptionStream = 6;

		public MinidumpAnalysis Read(String path)
		{
			var data = File.ReadAllBytes(path);
			if (data.Length < 32 || data[0] != 'M' || data[1] != 'D' || data[2] != 'M' || data[3] != 'P')
			{
				throw new InvalidDataException($"{path} is not a Windows minidump.");
			}

			var streams = ReadStreams(data);
			var modules = ReadModules(data, streams).OrderBy(module => module.BaseAddress).ToArray();
			var exception = ReadException(data, streams);
			var threads = ReadThreads(data, streams);
			return new MinidumpAnalysis(path, exception, modules, threads);
		}

		private static Dictionary<Int32, StreamDirectory> ReadStreams(Byte[] data)
		{
			var count = ReadInt32(data, 8);
			var directoryRva = ReadInt32(data, 12);
			var streams = new Dictionary<Int32, StreamDirectory>();

			for (var index = 0; index < count; index++)
			{
				var offset = directoryRva + index * 12;
				var type = ReadInt32(data, offset);
				var size = ReadUInt32(data, offset + 4);
				var rva = ReadUInt32(data, offset + 8);
				if (type != 0)
				{
					streams[type] = new StreamDirectory(size, rva);
				}
			}

			return streams;
		}

		private static List<ModuleRecord> ReadModules(Byte[] data, IReadOnlyDictionary<Int32, StreamDirectory> streams)
		{
			if (!streams.TryGetValue(ModuleListStream, out var stream))
			{
				return [];
			}

			var count = ReadInt32(data, (Int32)stream.Rva);
			var offset = (Int32)stream.Rva + 4;
			var modules = new List<ModuleRecord>(count);

			for (var index = 0; index < count; index++)
			{
				var baseAddress = ReadUInt64(data, offset);
				var size = ReadUInt32(data, offset + 8);
				var nameRva = ReadUInt32(data, offset + 20);
				var name = ReadUtf16String(data, (Int32)nameRva);
				modules.Add(new ModuleRecord(baseAddress, baseAddress + size, size, name));
				offset += 108;
			}

			return modules;
		}

		private static ExceptionRecord? ReadException(Byte[] data, IReadOnlyDictionary<Int32, StreamDirectory> streams)
		{
			if (!streams.TryGetValue(ExceptionStream, out var stream))
			{
				return null;
			}

			var rva = (Int32)stream.Rva;
			var exceptionRecord = rva + 8;
			var parameterCount = Math.Min(ReadInt32(data, exceptionRecord + 24), 15);
			var parameters = new List<UInt64>(parameterCount);

			for (var index = 0; index < parameterCount; index++)
			{
				parameters.Add(ReadUInt64(data, exceptionRecord + 32 + index * 8));
			}

			var contextRva = ReadUInt32(data, rva + 164);
			var contextSize = ReadUInt32(data, rva + 160);
			var registers = ReadX86Registers(data, contextRva, contextSize);

			return new ExceptionRecord(
				ReadInt32(data, rva),
				ReadUInt32(data, exceptionRecord),
				ReadUInt64(data, exceptionRecord + 16),
				parameters,
				contextRva,
				contextSize,
				registers);
		}

		private static List<ThreadRecord> ReadThreads(Byte[] data, IReadOnlyDictionary<Int32, StreamDirectory> streams)
		{
			if (!streams.TryGetValue(ThreadListStream, out var stream))
			{
				return [];
			}

			var count = ReadInt32(data, (Int32)stream.Rva);
			var offset = (Int32)stream.Rva + 4;
			var threads = new List<ThreadRecord>(count);

			for (var index = 0; index < count; index++)
			{
				var threadId = ReadInt32(data, offset);
				var stackStart = ReadUInt64(data, offset + 24);
				var stackSize = ReadUInt32(data, offset + 32);
				var stackRva = ReadUInt32(data, offset + 36);
				var contextSize = ReadUInt32(data, offset + 40);
				var contextRva = ReadUInt32(data, offset + 44);
				var registers = ReadX86Registers(data, contextRva, contextSize);

				threads.Add(new ThreadRecord(threadId, stackStart, stackSize, stackRva, registers));
				offset += 48;
			}

			return threads;
		}

		public static IReadOnlyDictionary<String, UInt32> ReadX86Registers(Byte[] data, UInt32 contextRva, UInt32 contextSize)
		{
			if (contextRva == 0 || contextSize < 0xCC || contextRva + 0xCC > data.Length)
			{
				return new Dictionary<String, UInt32>();
			}

			var rva = (Int32)contextRva;
			return new Dictionary<String, UInt32>
			{
				["EAX"] = ReadUInt32(data, rva + 0xB0),
				["EBX"] = ReadUInt32(data, rva + 0xA4),
				["ECX"] = ReadUInt32(data, rva + 0xAC),
				["EDX"] = ReadUInt32(data, rva + 0xA8),
				["ESI"] = ReadUInt32(data, rva + 0xA0),
				["EDI"] = ReadUInt32(data, rva + 0x9C),
				["EBP"] = ReadUInt32(data, rva + 0xB4),
				["EIP"] = ReadUInt32(data, rva + 0xB8),
				["ESP"] = ReadUInt32(data, rva + 0xC4),
				["EFLAGS"] = ReadUInt32(data, rva + 0xC0)
			};
		}

		private static String ReadUtf16String(Byte[] data, Int32 rva)
		{
			if (rva <= 0 || rva + 4 > data.Length)
			{
				return String.Empty;
			}

			var byteCount = ReadInt32(data, rva);
			if (byteCount <= 0 || rva + 4 + byteCount > data.Length)
			{
				return String.Empty;
			}

			return System.Text.Encoding.Unicode.GetString(data, rva + 4, byteCount);
		}

		private static Int32 ReadInt32(Byte[] data, Int32 offset)
		{
			return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
		}

		private static UInt32 ReadUInt32(Byte[] data, Int32 offset)
		{
			return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
		}

		private static UInt64 ReadUInt64(Byte[] data, Int32 offset)
		{
			return BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));
		}

		private sealed record StreamDirectory(UInt32 Size, UInt32 Rva);
	}
}