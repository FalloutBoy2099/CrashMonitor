namespace CrashMonitor.Core.SfallDebug
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Net.Sockets;
	using System.Text;
	using System.Threading;

	public sealed class SfallDebugClient : IDisposable
	{
		public const String DefaultHost = "127.0.0.1";
		public const Int32 DefaultPort = 4245;

		private readonly TcpClient _client;
		private readonly NetworkStream _stream;
		private readonly BinaryReader _reader;
		private readonly BinaryWriter _writer;
		private Boolean _disposed;
		private Boolean _sentExit;

		private SfallDebugClient(TcpClient client)
		{
			_client = client;
			_stream = client.GetStream();
			_reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);
			_writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
		}

		public static SfallDebugClient Connect(String host = DefaultHost, Int32 port = DefaultPort, Int32 startupDelayMilliseconds = 1000)
		{
			if (startupDelayMilliseconds > 0)
				Thread.Sleep(startupDelayMilliseconds);

			var client = new TcpClient();

			client.Connect(host, port);

			return new SfallDebugClient(client);
		}

		public SfallDebugSnapshot ReadSnapshot()
		{
			Int32 globalCount = _reader.ReadInt32();
			Int32 mapVariableCount = _reader.ReadInt32();
			Int32 sfallGlobalCount = _reader.ReadInt32();
			Int32 arrayCount = _reader.ReadInt32();
			Int32 critterCount = _reader.ReadInt32();

			IReadOnlyList<Int32> globals = ReadInt32List(globalCount);
			IReadOnlyList<Int32> mapVariables = ReadInt32List(mapVariableCount);

			List<SfallDebugGlobal> sfallGlobals = new(sfallGlobalCount);
			for (var index = 0; index < sfallGlobalCount; index++)
			{
				var key = _reader.ReadUInt64();
				var value = _reader.ReadInt32();
				_reader.ReadInt32();
				sfallGlobals.Add(new SfallDebugGlobal(key, FormatSfallGlobalKey(key), value));
			}

			List<SfallDebugArrayHeader> arrays = new(arrayCount);
			for (var index = 0; index < arrayCount; index++)
			{
				arrays.Add(new SfallDebugArrayHeader(
					_reader.ReadUInt32(),
					_reader.ReadInt32() != 0,
					_reader.ReadInt32(),
					_reader.ReadInt32()));
			}

			List<SfallDebugCritter> critters = new(critterCount);
			for (var index = 0; index < critterCount; index++)
			{
				var fid = _reader.ReadUInt32();
				var pointer = _reader.ReadUInt32();
				critters.Add(new SfallDebugCritter(index, fid, pointer, null));
			}

			return new SfallDebugSnapshot(globals, mapVariables, sfallGlobals, arrays, critters);
		}

		public IReadOnlyList<SfallDebugArrayItem> GetArrayValues(Int32 arrayIndex, SfallDebugArrayHeader header)
		{
			WriteCommand(SfallDebugCommand.GetArray);
			_writer.Write(arrayIndex);
			_writer.Flush();

			List<(SfallDebugArrayValueType Type, Int32 Length)> types = new(header.Length);
			var totalLength = 0;
			for (var index = 0; index < header.Length; index++)
			{
				SfallDebugArrayValueType type = (SfallDebugArrayValueType)_reader.ReadInt32();
				var length = _reader.ReadInt32();
				types.Add((type, length));
				totalLength += Math.Max(0, length);
			}

			var data = ReadExactBytes(totalLength);
			using MemoryStream stream = new(data);
			using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);

			List<SfallDebugArrayItem> values = new(header.Length);
			for (var index = 0; index < types.Count; index++)
			{
				(SfallDebugArrayValueType Type, Int32 Length) item = types[index];
				values.Add(new SfallDebugArrayItem(index, item.Type, item.Length, ReadArrayValue(reader, item.Type, item.Length)));
			}

			return values;
		}

		public SfallDebugCritterDetails GetCritterDetails(Int32 critterIndex)
		{
			WriteCommand(SfallDebugCommand.GetCritter);
			_writer.Write(critterIndex);
			_writer.Flush();

			List<Int32> fields = new(33);
			for (var index = 0; index < 33; index++)
				fields.Add(_reader.ReadInt32());

			return new SfallDebugCritterDetails(critterIndex, fields);
		}

		public SfallDebugLocalVariables GetLocalVariables(Int32 scriptId)
		{
			WriteCommand(SfallDebugCommand.GetLocalVariables);
			_writer.Write(scriptId);
			_writer.Flush();

			var count = _reader.ReadInt32();
			return new SfallDebugLocalVariables(scriptId, ReadInt32List(count));
		}

		public void SetGlobal(Int32 index, Int32 value)
		{
			WriteIndexedValue(SfallDebugCommand.SetGlobal, index, value);
		}

		public void SetMapVariable(Int32 index, Int32 value)
		{
			WriteIndexedValue(SfallDebugCommand.SetMapVariable, index, value);
		}

		public void SetSfallGlobal(Int32 index, Int32 value)
		{
			WriteIndexedValue(SfallDebugCommand.SetSfallGlobal, index, value);
		}

		public void Exit()
		{
			if (_sentExit)
				return;

			WriteCommand(SfallDebugCommand.Exit);
			_writer.Flush();
			_sentExit = true;
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_reader.Dispose();
			_writer.Dispose();
			_stream.Dispose();
			_client.Dispose();
			_disposed = true;
		}

		private IReadOnlyList<Int32> ReadInt32List(Int32 count)
		{
			List<Int32> values = new(Math.Max(0, count));
			for (var index = 0; index < count; index++)
				values.Add(_reader.ReadInt32());

			return values;
		}

		private Byte[] ReadExactBytes(Int32 count)
		{
			Byte[] buffer = new Byte[count];
			var offset = 0;
			while (offset < count)
			{
				var read = _stream.Read(buffer, offset, count - offset);
				if (read == 0)
					throw new EndOfStreamException("sfall debug connection closed while reading data.");

				offset += read;
			}

			return buffer;
		}

		private static String ReadArrayValue(BinaryReader reader, SfallDebugArrayValueType type, Int32 length)
		{
			return type switch
			{
				SfallDebugArrayValueType.Int => reader.ReadInt32().ToString(CultureInfo.InvariantCulture),
				SfallDebugArrayValueType.Float => reader.ReadSingle().ToString(CultureInfo.InvariantCulture),
				SfallDebugArrayValueType.String => ReadNullTerminatedString(reader, length),
				_ => SkipUnknown(reader, length)
			};
		}

		private static String ReadNullTerminatedString(BinaryReader reader, Int32 length)
		{
			var bytes = reader.ReadBytes(Math.Max(0, length));
			var end = Array.IndexOf(bytes, (Byte)0);
			if (end < 0)
				end = bytes.Length;

			return Encoding.ASCII.GetString(bytes, 0, end);
		}

		private static String SkipUnknown(BinaryReader reader, Int32 length)
		{
			_ = reader.ReadBytes(Math.Max(0, length));
			return String.Empty;
		}

		private static String FormatSfallGlobalKey(UInt64 key)
		{
			if (key > UInt32.MaxValue)
			{
				var bytes = BitConverter.GetBytes(key);
				var end = Array.IndexOf(bytes, (Byte)0);
				if (end < 0)
					end = bytes.Length;

				return Encoding.ASCII.GetString(bytes, 0, end);
			}

			return key.ToString(CultureInfo.InvariantCulture);
		}

		private void WriteIndexedValue(SfallDebugCommand command, Int32 index, Int32 value)
		{
			WriteCommand(command);
			_writer.Write(index);
			_writer.Write(value);
			_writer.Flush();
		}

		private void WriteCommand(SfallDebugCommand command)
		{
			_stream.WriteByte((Byte)command);
		}
	}
}