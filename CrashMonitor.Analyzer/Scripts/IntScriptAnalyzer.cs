namespace CrashMonitor.Analyzer.Scripts
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;

	public sealed class IntScriptAnalyzer
	{
		private static readonly IReadOnlyDictionary<UInt32, String> UnsafeOpcodes = new Dictionary<UInt32, String>
		{
			[0x8156] = "read_byte",
			[0x8157] = "read_short",
			[0x8158] = "read_int",
			[0x8159] = "read_string",
			[0x81CF] = "write_byte",
			[0x81D0] = "write_short",
			[0x81D1] = "write_int",
			[0x821B] = "write_string",
			[0x81D2] = "call_offset_v0",
			[0x81D3] = "call_offset_v1",
			[0x81D4] = "call_offset_v2",
			[0x81D5] = "call_offset_v3",
			[0x81D6] = "call_offset_v4",
			[0x81D7] = "call_offset_r0",
			[0x81D8] = "call_offset_r1",
			[0x81D9] = "call_offset_r2",
			[0x81DA] = "call_offset_r3",
			[0x81DB] = "call_offset_r4"
		};

		private static readonly IReadOnlyDictionary<UInt32, String> KnownOpcodes = new Dictionary<UInt32, String>
		{
			[0x8004] = "jump",
			[0x8005] = "call",
			[0x800C] = "a_to_d",
			[0x800D] = "d_to_a",
			[0x800E] = "exit",
			[0x8010] = "exit_program",
			[0x8012] = "fetch_global",
			[0x8013] = "store_global",
			[0x8019] = "swapa",
			[0x801A] = "pop",
			[0x801B] = "dup",
			[0x801C] = "return",
			[0x8029] = "pop_base",
			[0x802A] = "pop_to_base",
			[0x802B] = "push_base",
			[0x802F] = "if",
			[0x8031] = "store",
			[0x8032] = "fetch",
			[0x8033] = "eq",
			[0x8034] = "neq",
			[0x8035] = "lte",
			[0x8036] = "gte",
			[0x8037] = "lt",
			[0x8038] = "gt",
			[0x8039] = "add",
			[0x803A] = "sub",
			[0x803B] = "mul",
			[0x803C] = "div",
			[0x803D] = "mod",
			[0x803E] = "and",
			[0x803F] = "or",
			[0x8045] = "not",
			[0x8046] = "neg",
			[0x816A] = "set_global_script_repeat",
			[0x81AC] = "get_ini_setting",
			[0x81EA] = "init_hook",
			[0x81EB] = "get_ini_string",
			[0x819B] = "set_global_script_type",
			[0x8207] = "register_hook",
			[0x8276] = "string_format",
			[0x8277] = "set_cursor_mode"
		};

		public IntScriptAnalysis Analyze(String path)
		{
			var data = File.ReadAllBytes(path);
			if (data.Length < 46)
			{
				throw new InvalidDataException($"{path} is too small to be a Fallout INT script.");
			}

			var procedures = ReadProcedures(data);
			var staticStrings = ReadStaticStrings(data, procedures.Count);
			var unsafeCalls = FindUnsafeCalls(data, procedures, staticStrings.BaseOffset);

			return new IntScriptAnalysis(path, procedures, unsafeCalls, staticStrings.Values);
		}

		private static IReadOnlyList<IntScriptProcedure> ReadProcedures(Byte[] data)
		{
			var procedureCount = checked((Int32)ReadUInt32(data, 0x2A));
			if (procedureCount <= 0 || procedureCount > 2000)
				throw new InvalidDataException($"Invalid procedure count: {procedureCount}.");

			var identifierTableOffset = 0x2A + 4 + procedureCount * 24;
			var procedures = new List<IntScriptProcedure>(procedureCount);

			for (var index = 0; index < procedureCount; index++)
			{
				var offset = 0x2A + 4 + index * 24;
				var nameOffset = ReadUInt32(data, offset);
				var flags = ReadUInt32(data, offset + 4);
				var conditionOffset = ReadUInt32(data, offset + 12);
				var bodyOffset = ReadUInt32(data, offset + 16);
				var argumentCount = checked((Int32)ReadUInt32(data, offset + 20));
				var name = ReadIdentifier(data, identifierTableOffset, nameOffset);
				procedures.Add(new IntScriptProcedure(index, name, bodyOffset, conditionOffset, flags, argumentCount));
			}

			return procedures;
		}

		private static StaticStringTable ReadStaticStrings(Byte[] data, Int32 procedureCount)
		{
			var identifierTableOffset = 0x2A + 4 + procedureCount * 24;
			var identifierTableSize = checked((Int32)ReadUInt32(data, identifierTableOffset));

			var staticTableOffset = identifierTableOffset + 4 + identifierTableSize + 4;
			if (staticTableOffset + 4 > data.Length)
				return new StaticStringTable((UInt32)staticTableOffset, []);

			var staticTableSize = checked((Int32)ReadUInt32(data, staticTableOffset));
			var values = new List<String>();
			var offset = staticTableOffset + 4;
			var end = Math.Min(data.Length, offset + staticTableSize);

			while (offset + 2 <= end)
			{
				var length = ReadUInt16(data, offset);
				if (length == 0 || length > 4096 || offset + 2 + length > end)
					break;

				values.Add(ReadAscii(data, offset + 2));
				offset += 2 + length;
			}

			return new StaticStringTable((UInt32)(staticTableOffset + 4), values);
		}

		private static IReadOnlyList<IntScriptUnsafeCall> FindUnsafeCalls(Byte[] data, IReadOnlyList<IntScriptProcedure> procedures, UInt32 staticStringBaseOffset)
		{
			var bodyOffsets = procedures
				.Where(procedure => procedure.BodyOffset > 0)
				.Select(procedure => procedure.BodyOffset)
				.Order()
				.ToArray();

			var unsafeCalls = new List<IntScriptUnsafeCall>();

			foreach (var procedure in procedures.Where(procedure => procedure.BodyOffset > 0))
			{
				var endOffset = bodyOffsets.FirstOrDefault(offset => offset > procedure.BodyOffset);
				if (endOffset == 0)
					endOffset = (UInt32)data.Length;

				var context = new Queue<IntScriptInstruction>();
				var offset = checked((Int32)procedure.BodyOffset);

				while (offset + 2 <= endOffset && offset + 2 <= data.Length)
				{
					var instruction = DecodeInstruction(data, offset, staticStringBaseOffset);
					context.Enqueue(instruction);
					while (context.Count > 12)
						context.Dequeue();

					if (UnsafeOpcodes.ContainsKey(instruction.Opcode))
					{
						var contextArray = context.ToArray();
						var argumentContext = GetCurrentUnsafeArgumentContext(contextArray);

						var pushInts = argumentContext
							.Where(item => item.Mnemonic == "push_int" && item.Operand is not null)
							.Select(item => item.Operand!.Value)
							.ToArray();

						var directAddress = pushInts.FirstOrDefault(value => value >= 0x00400000);
						var firstArgument = pushInts.LastOrDefault(value => value < 0x00400000);

						unsafeCalls.Add(new IntScriptUnsafeCall(
							procedure,
							instruction,
							contextArray,
							directAddress == 0 ? null : directAddress,
							firstArgument == 0 ? null : firstArgument));
					}

					offset += InstructionSize(instruction);
				}
			}

			return unsafeCalls;
		}

		private static IReadOnlyList<IntScriptInstruction> GetCurrentUnsafeArgumentContext(IReadOnlyList<IntScriptInstruction> context)
		{
			for (var index = context.Count - 2; index >= 0; index--)
				if (UnsafeOpcodes.ContainsKey(context[index].Opcode))
					return context.Skip(index + 1).ToArray();

			return context;
		}

		private static IntScriptInstruction DecodeInstruction(Byte[] data, Int32 offset, UInt32 staticStringBaseOffset)
		{
			var opcode = ReadUInt16(data, offset);
			if (IsTypedPush(opcode) && offset + 6 <= data.Length)
			{
				var operand = ReadUInt32(data, offset + 2);
				if (opcode == 0xC001)
				{
					return new IntScriptInstruction((UInt32)offset, opcode, "push_int", operand, FormatHex(operand));
				}

				if (opcode == 0x9001)
				{
					var absolute = checked((Int32)(staticStringBaseOffset + operand));
					var text = absolute >= 0 && absolute < data.Length ? ReadAscii(data, absolute) : "";
					return new IntScriptInstruction((UInt32)offset, opcode, "push_string", operand, text);
				}

				return new IntScriptInstruction((UInt32)offset, opcode, $"push_{FormatHex(opcode, 4)}", operand, FormatHex(operand));
			}

			var mnemonic = UnsafeOpcodes.TryGetValue(opcode, out var unsafeName)
				? unsafeName
				: KnownOpcodes.GetValueOrDefault(opcode, $"op_{FormatHex(opcode, 4)}");

			return new IntScriptInstruction((UInt32)offset, opcode, mnemonic, null, null);
		}

		private static Int32 InstructionSize(IntScriptInstruction instruction)
		{
			return IsTypedPush(instruction.Opcode) ? 6 : 2;
		}

		private static Boolean IsTypedPush(UInt32 opcode)
		{
			return opcode is 0xC001 or 0xA001 or 0x9001 or 0x9801 or 0xE001;
		}

		private static String ReadIdentifier(Byte[] data, Int32 tableOffset, UInt32 nameOffset)
		{
			var stringOffset = checked(tableOffset + (Int32)nameOffset);
			return ReadAscii(data, stringOffset);
		}

		private static String ReadAscii(Byte[] data, Int32 offset)
		{
			if (offset < 0 || offset >= data.Length)
				return String.Empty;

			var end = offset;
			while (end < data.Length && data[end] != 0)
				end++;

			return Encoding.Latin1.GetString(data, offset, end - offset);
		}

		private static UInt16 ReadUInt16(Byte[] data, Int32 offset)
		{
			return (UInt16)((data[offset] << 8) | data[offset + 1]);
		}

		private static UInt32 ReadUInt32(Byte[] data, Int32 offset)
		{
			return ((UInt32)data[offset] << 24)
				| ((UInt32)data[offset + 1] << 16)
				| ((UInt32)data[offset + 2] << 8)
				| data[offset + 3];
		}

		private static String FormatHex(UInt32 value, Int32 width = 8)
		{
			return $"0x{value.ToString($"X{width}")}";
		}

		private sealed record StaticStringTable(UInt32 BaseOffset, IReadOnlyList<String> Values);
	}
}