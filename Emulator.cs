namespace cs8080
{
	class ConditionCodes
	{
		// this is 1 8-bit register containing 5 1 bit flags
		public bool S = false, Z = false, P = false, CY = false, AC = false;
	}

	class State : ConditionCodes
	{
		private const ushort MaxMem = 65535; // 64k of ram
		public byte B = 0x0, C = 0x0, D = 0, E = 0, H = 0x0, L = 0x0, A = 0x0; // 7 8-bit general purpose registers (can be used as 16 bit in pairs of 2) and a special accumulator register
		public ushort SP = 0, PC = 0; // stack pointer that keeps return address, program counter that loads the next instruction to execute
		public byte[] mem8080 = new byte[MaxMem]; // 64k of ram as a byte array
	}

	class Ops
	{
		protected static byte nextByte(byte[] mem, ushort PC)
		{
			return mem[PC + 1];
		}

		protected static byte next2Byte(byte[] mem, ushort PC)
		{
			return mem[PC + 2];
		}

		protected static ushort toWord(byte hi/* left byte */, byte lo/* right byte */) // hi lo is big endian but whatever
		{
			return (ushort)(hi << 8 | lo);
		}

		protected static ushort nextWord(byte[] mem, ushort PC)
		{
			return toWord(mem[PC + 2], mem[PC + 1]);
		}

		protected static byte splitWordlo(ushort word)
		{
			return (byte)word;
		}

		protected static byte splitWordhi(ushort word)
		{
			return (byte)(word >> 8);
		}
	}

	class ArithFlags : Ops
	{
		public static bool ZeroF = false, SignF = false, ParityF = false, CarryF = false, AuxCarryF = false;
		private static bool Zero(ushort ans)
		{
			return (ans & 0xff) == 0;
		}

		private static bool Sign(ushort ans)
		{
			return Convert.ToBoolean((ans & 0xff) >> 7);
		}

		private static bool Parity(ushort ans) // left shift by 1 and add 0 if 0 and 1 if 1, then keep left shifting 1
		{
			byte oddcount = 0;
			for (byte i = 1; i <= 8; i++)
			{
				ans = (ushort)(ans << 1);
				oddcount += splitWordhi(ans);
				ans = splitWordlo(ans);
			}
			return oddcount % 2 != 0;
		}

		private static bool Carry(ushort ans)
		{
			return ans > 0xff;
		}

		private static bool AuxCarry(ushort ans)
		{
			return (byte)((byte)(ans & 0xff) & 0b00011111) > 0xf;
		}

		public static State Set(State i8080, ushort ans)
		{
			i8080.Z = ZeroF & Zero(ans);
			i8080.S = SignF & Sign(ans);
			i8080.P = ParityF & Parity(ans);
			i8080.CY = CarryF & Carry(ans);
			i8080.AC = AuxCarryF & AuxCarry(ans);
			return i8080;
		}

		public static byte B2f(State i8080)
		{
			byte flags;
			flags = Convert.ToByte(i8080.CY);
			flags |= 1 << 1;
      	flags |= (byte)(Convert.ToByte(i8080.P) << 2);
			flags |= (byte)(Convert.ToByte(i8080.AC) << 4);
			flags |= (byte)(Convert.ToByte(i8080.Z) << 6);
			flags |= (byte)(Convert.ToByte(i8080.S) << 7);
			return flags;
		}
	}

	class Instructions : Ops
	{
		protected static State INR(State i8080)
		{
			ArithFlags.ZeroF = true;
			ArithFlags.SignF = true;
			ArithFlags.ParityF = true;
			return i8080;
		}

		protected static State DCR(State i8080)
		{
			ArithFlags.ZeroF = true;
			ArithFlags.SignF = true;
			ArithFlags.ParityF = true;
			return i8080;
		}

		protected static State DAD(State i8080, int addpair) // 32 bit int because we need 1 overflow bit for carry
		{
			int resultpair = addpair + toWord(i8080.H, i8080.L);
			i8080.CY = Convert.ToBoolean(resultpair >> 16); // right shift by 16 and if anything is left, carry bit is set to 1
			i8080.H = splitWordhi((ushort)resultpair);
			i8080.L = splitWordlo((ushort)resultpair);
			return i8080;
		}

		protected static State DAA(State i8080)
		{
			ArithFlags.ZeroF = true;
			ArithFlags.SignF = true;
			ArithFlags.ParityF = true;
			return i8080;
		}

		protected static State ADD(State i8080)
		{
			ArithFlags.ZeroF = true;
			ArithFlags.SignF = true;
			ArithFlags.ParityF = true;
			ArithFlags.CarryF = true;
			ArithFlags.AuxCarryF = true;
			return i8080;
		}

		protected static State ADC(State i8080)
		{
			ArithFlags.ZeroF = true;
			ArithFlags.SignF = true;
			ArithFlags.ParityF = true;
			ArithFlags.CarryF = true;
			ArithFlags.AuxCarryF = true;
			return i8080;
		}

		protected static State SUB(State i8080, ushort ans)
		{
			ArithFlags.ZeroF = true;
			ArithFlags.SignF = true;
			ArithFlags.ParityF = true;
			ArithFlags.CarryF = true;
			ArithFlags.AuxCarryF = true;
			return i8080;
		}

		protected static State SBB(State i8080, ushort ans)
		{
			ArithFlags.ZeroF = true;
			ArithFlags.SignF = true;
			ArithFlags.ParityF = true;
			ArithFlags.CarryF = true;
			ArithFlags.AuxCarryF = true;
			return i8080;
		}

		protected static State RET(State i8080)
		{
			i8080.PC = toWord(i8080.mem8080[i8080.SP + 1], i8080.mem8080[i8080.SP]);
			i8080.SP += 2;
			return i8080;
		}

		protected static State JMP(State i8080, ushort adr)
		{
			i8080.PC = (ushort)(adr - 1); // Executor always increments PC by 1 so undo that 
			return i8080;
		}

		protected static State CALL(State i8080, ushort adr)
		{
			i8080.mem8080[i8080.SP - 1] = splitWordhi(i8080.PC);
			i8080.mem8080[i8080.SP - 2] = splitWordlo(i8080.PC);
			i8080.SP -= 2;
			JMP(i8080, adr);
			return i8080;
		}
	}

	class Emulate : Instructions
	{
		private static State OpcodeHandler(State i8080)
		{
			byte nbyte = nextByte(i8080.mem8080, i8080.PC), nbyte2 = next2Byte(i8080.mem8080, i8080.PC), leftbit, rightbit, ahl = i8080.mem8080[toWord(i8080.H, i8080.L)];
			ushort nword = nextWord(i8080.mem8080, i8080.PC), regpair; // we use regpair because it made sense for a few things, and we can reuse this for other ushort var requirements
			switch (i8080.mem8080[i8080.PC])
			{
				// NOP instruction, do nothing
				case 0x00 or 0x08 or 0x10 or 0x18 or 0x20 or 0x28 or 0x30 or 0x38 or 0xcb or 0xd9 or 0xdd or 0xed or 0xfd: break;
				// LXI instruction, load pc+1 and pc+2 into a register pair
				case 0x01: i8080.B = nbyte2; i8080.C = nbyte; i8080.PC += 2; break;
				case 0x11: i8080.D = nbyte2; i8080.E = nbyte; i8080.PC += 2; break;
				case 0x21: i8080.H = nbyte2; i8080.L = nbyte; i8080.PC += 2; break;
				case 0x31: i8080.SP = nword; i8080.PC += 2; break;
				// STAX instruction, store accumulator into mem, address indicated by register pair
				case 0x02: i8080.mem8080[toWord(i8080.B, i8080.C)] = i8080.A; break;
				case 0x12: i8080.mem8080[toWord(i8080.D, i8080.E)] = i8080.A; break;
				// INX instruction, join registers to word, then increment, then split word
				case 0x03: regpair = toWord(i8080.B, i8080.C); regpair++; i8080.B = splitWordhi(regpair); i8080.C = splitWordlo(regpair); break;
				case 0x13: regpair = toWord(i8080.D, i8080.E); regpair++; i8080.D = splitWordhi(regpair); i8080.E = splitWordlo(regpair); break;
				case 0x23: regpair = toWord(i8080.H, i8080.L); regpair++; i8080.H = splitWordhi(regpair); i8080.L = splitWordlo(regpair); break;
				case 0x33: i8080.SP++; break;
				// INR instruction, increment a register
				case 0x04: i8080.B++; break;
				case 0x0c: i8080.C++; break;
				case 0x14: i8080.D++; break;
				case 0x1c: i8080.E++; break;
				case 0x24: i8080.H++; break;
				case 0x2c: i8080.L++; break;
				case 0x34: i8080.mem8080[toWord(i8080.H, i8080.L)] = (byte)(ahl + 1); break;
				case 0x3c: i8080.A++; break;
				// DCR instruction, decrement a register
				case 0x05: i8080.B--; break;
				case 0x0d: i8080.C--; break;
				case 0x15: i8080.D--; break;
				case 0x1d: i8080.E--; break;
				case 0x25: i8080.H--; break;
				case 0x2d: i8080.L--; break;
				case 0x35: i8080.mem8080[toWord(i8080.H, i8080.L)] = (byte)(ahl - 1); break;
				case 0x3d: i8080.A--; break;
				// MVI instruction, loads register with next byte of the ROM
				case 0x06: i8080.B = nbyte; i8080.PC++; break;
				case 0x0e: i8080.C = nbyte; i8080.PC++; break;
				case 0x16: i8080.D = nbyte; i8080.PC++; break;
				case 0x1e: i8080.E = nbyte; i8080.PC++; break;
				case 0x26: i8080.H = nbyte; i8080.PC++; break;
				case 0x2e: i8080.L = nbyte; i8080.PC++; break;
				case 0x36: i8080.mem8080[toWord(i8080.H, i8080.L)] = nbyte; i8080.PC++; break;
				case 0x3e: i8080.A = nbyte; i8080.PC++; break;
				// RLC, rotates the accumulator left bitwise, carry is set to high order bit of A
				case 0x07:
					leftbit = (byte)(i8080.A >> 7);
					i8080.CY = Convert.ToBoolean(leftbit);
					i8080.A = (byte)((byte)(i8080.A << 1) | leftbit);
					break;
				// DAD, register pair is added to HL
				case 0x09: DAD(i8080, toWord(i8080.B, i8080.C)); break;
				case 0x19: DAD(i8080, toWord(i8080.D, i8080.E)); break;
				case 0x29: DAD(i8080, toWord(i8080.H, i8080.L)); break;
				case 0x39: DAD(i8080, i8080.SP); break;
				// LDAX, set accumulator to mem address of register pair BC or DE
				case 0x0a: i8080.A = i8080.mem8080[toWord(i8080.B, i8080.C)]; break;
				case 0x1a: i8080.A = i8080.mem8080[toWord(i8080.D, i8080.E)]; break;
				// DCX instruction
				case 0x0b: regpair = toWord(i8080.B, i8080.C); regpair++; i8080.B = splitWordhi(regpair); i8080.C = splitWordlo(regpair); break;
				case 0x1b: regpair = toWord(i8080.D, i8080.E); regpair++; i8080.D = splitWordhi(regpair); i8080.E = splitWordlo(regpair); break;
				case 0x2b: regpair = toWord(i8080.H, i8080.L); regpair++; i8080.H = splitWordhi(regpair); i8080.L = splitWordlo(regpair); break;
				case 0x3b: i8080.SP++; break;
				// RRC, rotates accumulator right, so RLC but right
				case 0x0f:
					rightbit = (byte)(i8080.A << 7);
					i8080.CY = Convert.ToBoolean((byte)(rightbit >> 7));
					i8080.A = (byte)((byte)(i8080.A >> 1) | rightbit);
					break;
				// RAL, RLC but carry bit is used for the lo bit and is set to hi bit
				case 0x17:
					leftbit = (byte)(i8080.A >> 7);
					i8080.A = (byte)((byte)(i8080.A << 1) | Convert.ToByte(i8080.CY));
					i8080.CY = Convert.ToBoolean(leftbit);
					break;
				// RAR, if RRC & RAL had a child
				case 0x1f:
					rightbit = (byte)(i8080.A << 7);
					i8080.A = (byte)((byte)(i8080.A >> 1) | Convert.ToByte(i8080.CY));
					i8080.CY = Convert.ToBoolean((byte)(rightbit >> 7));
					break;
				// SHLD, L and H register stored at adr operand and adr operand + 1
				case 0x22:
					i8080.mem8080[nword] = i8080.L;
					i8080.mem8080[nword + 1] = i8080.H;
					i8080.PC += 2;
					break;
				// DAA, accumulator's 8 bit number is turned into 2 4-bit binary-coded-decimal digits (w h a t)
				case 0x27:
					if ((byte)(i8080.A & 0x0f) > 0x09 || i8080.AC)
					{
						i8080.A += 0x06;
						i8080.AC = (byte)(i8080.A & 0x0f) + 0x06 > 0x10;
					}
					if ((byte)(i8080.A & 0xf0) > 0x90 || i8080.CY)
					{
						i8080.A = (byte)(i8080.A + 0x60 & 0xff);
					}
					break;
				// LHLD, SHLD but read instead of write
				case 0x2a:
					i8080.L = i8080.mem8080[nword];
					i8080.H = i8080.mem8080[nword+1];
					i8080.PC += 2;
					break;
				// CMA, bitflip accumulator
				case 0x2f: i8080.A = (byte)~i8080.A; break;
				// STA, store accumulator at the adr operand
				case 0x32: i8080.mem8080[nword] = i8080.A; i8080.PC += 2; break;
				// STC, set carry flag
				case 0x37: i8080.CY = true; break;
				// LDA, STA but adr operand's byte is written to the accumulator
				case 0x3a: i8080.A = i8080.mem8080[nword]; i8080.PC += 2; break;
				// CMC, carry flag inversed
				case 0x3f: i8080.CY = !i8080.CY; break;
				// MOV, copy one register's value to another
				case 0x40: i8080.B = i8080.B; break;
				case 0x41: i8080.B = i8080.C; break;
				case 0x42: i8080.B = i8080.D; break;
				case 0x43: i8080.B = i8080.E; break;
				case 0x44: i8080.B = i8080.H; break;
				case 0x45: i8080.B = i8080.L; break;
				case 0x46: i8080.B = ahl; break;
				case 0x47: i8080.B = i8080.A; break;
				case 0x48: i8080.C = i8080.B; break;
				case 0x49: i8080.C = i8080.C; break;
				case 0x4a: i8080.C = i8080.D; break;
				case 0x4b: i8080.C = i8080.E; break;
				case 0x4c: i8080.C = i8080.H; break;
				case 0x4d: i8080.C = i8080.L; break;
				case 0x4e: i8080.C = ahl; break;
				case 0x4f: i8080.C = i8080.A; break;
				case 0x50: i8080.D = i8080.B; break;
				case 0x51: i8080.D = i8080.C; break;
				case 0x52: i8080.D = i8080.D; break;
				case 0x53: i8080.D = i8080.E; break;
				case 0x54: i8080.D = i8080.H; break;
				case 0x55: i8080.D = i8080.L; break;
				case 0x56: i8080.D = ahl; break;
				case 0x57: i8080.D = i8080.A; break;
				case 0x58: i8080.E = i8080.B; break;
				case 0x59: i8080.E = i8080.C; break;
				case 0x5a: i8080.E = i8080.D; break;
				case 0x5b: i8080.E = i8080.E; break;
				case 0x5c: i8080.E = i8080.H; break;
				case 0x5d: i8080.E = i8080.L; break;
				case 0x5e: i8080.E = ahl; break;
				case 0x5f: i8080.H = i8080.A; break;
				case 0x60: i8080.H = i8080.B; break;
				case 0x61: i8080.H = i8080.C; break;
				case 0x62: i8080.H = i8080.D; break;
				case 0x63: i8080.H = i8080.E; break;
				case 0x64: i8080.H = i8080.H; break;
				case 0x65: i8080.H = i8080.L; break;
				case 0x66: i8080.H = ahl; break;
				case 0x67: i8080.L = i8080.A; break;
				case 0x68: i8080.L = i8080.B; break;
				case 0x69: i8080.L = i8080.C; break;
				case 0x6a: i8080.L = i8080.D; break;
				case 0x6b: i8080.L = i8080.E; break;
				case 0x6c: i8080.L = i8080.H; break;
				case 0x6d: i8080.L = i8080.L; break;
				case 0x6e: i8080.L = ahl; break;
				case 0x6f: i8080.mem8080[toWord(i8080.H, i8080.L)] = i8080.A; break;
				case 0x70: i8080.mem8080[toWord(i8080.H, i8080.L)] = i8080.B; break;
				case 0x71: i8080.mem8080[toWord(i8080.H, i8080.L)] = i8080.C; break;
				case 0x72: i8080.mem8080[toWord(i8080.H, i8080.L)] = i8080.D; break;
				case 0x73: i8080.mem8080[toWord(i8080.H, i8080.L)] = i8080.E; break;
				case 0x74: i8080.mem8080[toWord(i8080.H, i8080.L)] = i8080.H; break;
				case 0x75: i8080.mem8080[toWord(i8080.H, i8080.L)] = i8080.L; break;
				case 0x76: Console.WriteLine("Halt requested, terminating execution..."); Environment.Exit(0); break; // HLT
				case 0x77: i8080.mem8080[toWord(i8080.H, i8080.L)] = i8080.A; break;
				case 0x78: i8080.A = i8080.B; break;
				case 0x79: i8080.A = i8080.C; break;
				case 0x7a: i8080.A = i8080.D; break;
				case 0x7b: i8080.A = i8080.E; break;
				case 0x7c: i8080.A = i8080.H; break;
				case 0x7d: i8080.A = i8080.L; break;
				case 0x7e: i8080.A = ahl; break;
				case 0x7f: i8080.A = i8080.A; break;
				// ADD, add a register to the accumulator
				case 0x80: i8080.A += i8080.B; break;
				case 0x81: i8080.A += i8080.C; break;
				case 0x82: i8080.A += i8080.D; break;
				case 0x83: i8080.A += i8080.E; break;
				case 0x84: i8080.A += i8080.H; break;
				case 0x85: i8080.A += i8080.L; break;
				case 0x86: i8080.A += ahl; break;
				case 0x87: i8080.A += i8080.A; break;
				// ADC, ADD but with a carry flag
				case 0x88: i8080.A += (byte)(i8080.B + Convert.ToByte(i8080.CY)); break;
				case 0x89: i8080.A += (byte)(i8080.C + Convert.ToByte(i8080.CY)); break;
				case 0x8a: i8080.A += (byte)(i8080.D + Convert.ToByte(i8080.CY)); break;
				case 0x8b: i8080.A += (byte)(i8080.E + Convert.ToByte(i8080.CY)); break;
				case 0x8c: i8080.A += (byte)(i8080.H + Convert.ToByte(i8080.CY)); break;
				case 0x8d: i8080.A += (byte)(i8080.L + Convert.ToByte(i8080.CY)); break;
				case 0x8e: i8080.A += (byte)(ahl + Convert.ToByte(i8080.CY)); break;
				case 0x8f: i8080.A += (byte)(i8080.A + Convert.ToByte(i8080.CY)); break;
				// SUB, convert the register to 2's complement then add
				case 0x90: regpair = (ushort)(i8080.A + (~i8080.B & 0xff) + 1); i8080.B = (byte)regpair; SUB(i8080, regpair); break;
				case 0x91: regpair = (ushort)(i8080.A + (~i8080.C & 0xff) + 1); i8080.C = (byte)regpair; SUB(i8080, regpair); break;
				case 0x92: regpair = (ushort)(i8080.A + (~i8080.D & 0xff) + 1); i8080.D = (byte)regpair; SUB(i8080, regpair); break;
				case 0x93: regpair = (ushort)(i8080.A + (~i8080.E & 0xff) + 1); i8080.E = (byte)regpair; SUB(i8080, regpair); break;
				case 0x94: regpair = (ushort)(i8080.A + (~i8080.H & 0xff) + 1); i8080.H = (byte)regpair; SUB(i8080, regpair); break;
				case 0x95: regpair = (ushort)(i8080.A + (~i8080.L & 0xff) + 1); i8080.L = (byte)regpair; SUB(i8080, regpair); break;
				case 0x96: regpair = (ushort)(i8080.A + (~ahl & 0xff) + 1); i8080.mem8080[toWord(i8080.H, i8080.L)] = (byte)regpair; SUB(i8080, regpair); break;
				case 0x97: i8080.A = 0x0; i8080.CY = false; break; // subbing A is very simple
				// SBB, SUB but with a carry added to the register
				case 0x98: regpair = (ushort)(i8080.A + (~(i8080.B + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.B = (byte)regpair; SBB(i8080, regpair); break;
				case 0x99: regpair = (ushort)(i8080.A + (~(i8080.C + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.C = (byte)regpair; SBB(i8080, regpair); break;
				case 0x9a: regpair = (ushort)(i8080.A + (~(i8080.D + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.D = (byte)regpair; SBB(i8080, regpair); break;
				case 0x9b: regpair = (ushort)(i8080.A + (~(i8080.E + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.E = (byte)regpair; SBB(i8080, regpair); break;
				case 0x9c: regpair = (ushort)(i8080.A + (~(i8080.H + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.H = (byte)regpair; SBB(i8080, regpair); break;
				case 0x9d: regpair = (ushort)(i8080.A + (~(i8080.L + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.L = (byte)regpair; SBB(i8080, regpair); break;
				case 0x9e: regpair = (ushort)(i8080.A + (~(ahl + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.mem8080[toWord(i8080.H, i8080.L)] = (byte)regpair; SBB(i8080, regpair); break;
				case 0x9f: regpair = (ushort)(i8080.A + (~(i8080.A + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.A = (byte)regpair; SBB(i8080, regpair); break;
				// ANA, accumulator logically AND'ed with specified reg
				case 0xa0: i8080.A = (byte)(i8080.A & i8080.B); break;
				case 0xa1: i8080.A = (byte)(i8080.A & i8080.C); break;
				case 0xa2: i8080.A = (byte)(i8080.A & i8080.D); break;
				case 0xa3: i8080.A = (byte)(i8080.A & i8080.E); break;
				case 0xa4: i8080.A = (byte)(i8080.A & i8080.H); break;
				case 0xa5: i8080.A = (byte)(i8080.A & i8080.L); break;
				case 0xa6: i8080.A = (byte)(i8080.A & ahl); break;
				case 0xa7: i8080.A = (byte)(i8080.A & i8080.A); break;
				// XRA, accumulator XOR'd 
				case 0xa8: i8080.A = (byte)(i8080.A ^ i8080.B); break;
				case 0xa9: i8080.A = (byte)(i8080.A ^ i8080.C); break;
				case 0xaa: i8080.A = (byte)(i8080.A ^ i8080.D); break;
				case 0xab: i8080.A = (byte)(i8080.A ^ i8080.E); break;
				case 0xac: i8080.A = (byte)(i8080.A ^ i8080.H); break;
				case 0xad: i8080.A = (byte)(i8080.A ^ i8080.L); break;
				case 0xae: i8080.A = (byte)(i8080.A ^ ahl); break;
				case 0xaf: i8080.A = (byte)(i8080.A ^ i8080.A); break;
				// ORA, accumulator OR'd
				case 0xb0: i8080.A = (byte)(i8080.A | i8080.B); break;
				case 0xb1: i8080.A = (byte)(i8080.A | i8080.C); break;
				case 0xb2: i8080.A = (byte)(i8080.A | i8080.D); break;
				case 0xb3: i8080.A = (byte)(i8080.A | i8080.E); break;
				case 0xb4: i8080.A = (byte)(i8080.A | i8080.H); break;
				case 0xb5: i8080.A = (byte)(i8080.A | i8080.L); break;
				case 0xb6: i8080.A = (byte)(i8080.A | ahl); break;
				case 0xb7: i8080.A = (byte)(i8080.A | i8080.A); break;
				// CMP, i think it is like SUB but only setting conditional flags
				case <= 0xbf and >= 0xb8: Console.WriteLine("Not implemented."); break;
				// RNZ
				case 0xc0: if (!i8080.Z) { RET(i8080); } break;
				// POP
				case 0xc1: i8080.C = i8080.mem8080[i8080.SP]; i8080.B = i8080.mem8080[i8080.SP + 1]; i8080.SP += 2; break;
				case 0xd1: i8080.E = i8080.mem8080[i8080.SP]; i8080.D = i8080.mem8080[i8080.SP + 1]; i8080.SP += 2; break;
				case 0xe1: i8080.L = i8080.mem8080[i8080.SP]; i8080.H = i8080.mem8080[i8080.SP + 1]; i8080.SP += 2; break;
				case 0xf1:
					i8080.CY = (i8080.SP & 1) > 0;
					i8080.AC = (i8080.SP & (1 << 4)) > 0;
					i8080.Z = (i8080.SP & (1 << 6)) > 0;
					i8080.S = (i8080.SP & (1 << 7)) > 0;
					i8080.A = i8080.mem8080[i8080.SP + 1];
					i8080.SP += 2;
					break;
				// JNZ
				case 0xc2: if (!i8080.Z) { JMP(i8080, nword); } break;
				// JMP, jump to address
				case 0xc3: JMP(i8080, nword); break; // i don't think we need to increment PC by 2 since PC gets set to something else anyway
				// CNZ
				case 0xc4: if (!i8080.Z) { CALL(i8080, nword); } break;
				// PUSH
				case 0xc5: i8080.mem8080[i8080.SP - 2] = i8080.C; i8080.mem8080[i8080.SP - 1] = i8080.B; i8080.SP -= 2; break;
				case 0xd5: i8080.mem8080[i8080.SP - 2] = i8080.E; i8080.mem8080[i8080.SP - 1] = i8080.D; i8080.SP -= 2; break;
				case 0xe5: i8080.mem8080[i8080.SP - 2] = i8080.L; i8080.mem8080[i8080.SP - 1] = i8080.H; i8080.SP -= 2; break;
				case 0xf5: i8080.mem8080[i8080.SP - 2] = ArithFlags.B2f(i8080); i8080.SP -= 2; break;
				// ADI
				case 0xc6: i8080.A += nbyte; i8080.PC++; break;
				// RST 0, 1, 2, 3, 4, 5, 6, 7
				case 0xc7: CALL(i8080, 0x00); break;
				case 0xcf: CALL(i8080, 0x08); break;
				case 0xd7: CALL(i8080, 0x10); break;
				case 0xdf: CALL(i8080, 0x18); break;
				case 0xe7: CALL(i8080, 0x20); break;
				case 0xef: CALL(i8080, 0x28); break;
				case 0xf7: CALL(i8080, 0x30); break;
				case 0xff: CALL(i8080, 0x38); break;
				// RZ
				case 0xc8: if (i8080.Z) { RET(i8080); } break;
				// RET, return op
				case 0xc9: RET(i8080); break;
				// JZ
				case 0xca: if (i8080.Z) { JMP(i8080, nword); } break;
				// CZ
				case 0xcc: if (i8080.Z) { CALL(i8080, nword); } break;
				// CALL
				case 0xcd: CALL(i8080, nword); break;
				// ACI
				case 0xce: i8080.A += (byte)(nbyte + Convert.ToByte(i8080.CY)); i8080.PC++; break;
				// RNC
				case 0xd0: if (!i8080.CY) { RET(i8080); } break;
				// JNC
				case 0xd2: if (!i8080.CY) { i8080.PC = (ushort)(nword - 1); } break;
				// OUT
				case 0xd3: Console.WriteLine("I/O not implemented."); break;
				// CNC
				case 0xd4: if (!i8080.CY) { CALL(i8080, nword); } break;
				// SUI
				case 0xd6: i8080.A -= nbyte; i8080.PC++; break;
				// RC
				case 0xd8: if (i8080.CY) { RET(i8080); } break;
				// JC
				case 0xda: if (i8080.CY) { i8080.PC = (ushort)(nword - 1); } break;
				// IN
				case 0xdb: Console.WriteLine("I/O not implemented."); break;
				// CC
				case 0xdc: if (i8080.CY) { CALL(i8080, nword); } break;
				// SBI
				case 0xde: i8080.A -= (byte)(nbyte - Convert.ToByte(i8080.CY)); i8080.PC++; break;
				// RPO
				case 0xe0: if (!i8080.P) { RET(i8080); } break;
				// JPO
				case 0xe2: if (!i8080.P) { i8080.PC = (ushort)(nword - 1); } break;
				// XTHL
				case 0xe3: (i8080.L, i8080.mem8080[i8080.SP]) = (i8080.mem8080[i8080.SP], i8080.L); (i8080.H, i8080.mem8080[i8080.SP+1]) = (i8080.mem8080[i8080.SP+1], i8080.H); break;
				// CPO
				case 0xe4: if (!i8080.P) { CALL(i8080, nword); } break;
				// ANI
				case 0xe6: i8080.A = (byte)(i8080.A & nbyte); i8080.PC++; break;
				// RPE
				case 0xe8: if (i8080.P) { RET(i8080); } break;
				// PCHL
				case 0xe9: i8080.PC = toWord(i8080.H, i8080.L); break;
				// JPE
				case 0xea: if (i8080.P) { i8080.PC = (ushort)(nword - 1); } break;
				// XCHG
				case 0xeb: (i8080.H, i8080.D) = (i8080.D, i8080.H); (i8080.L, i8080.E) = (i8080.E, i8080.L); break;
				// CPE
				case 0xec: if (i8080.P) { CALL(i8080, nword); } break;
				// XRI
				case 0xee: i8080.A = (byte)(i8080.A ^ nbyte); i8080.PC++; break;
				// RP
				case 0xf0: if (!i8080.S) { RET(i8080); } break;
				// JP
				case 0xf2: if (!i8080.S) { JMP(i8080, nword); } break;
				// DI, disable interrupts
				case 0xf3: Console.WriteLine("Not implemented."); break;
				// CP
				case 0xf4: if (!i8080.S) { CALL(i8080, nword); } break;
				// ORI
				case 0xf6: i8080.A = (byte)(i8080.A | nbyte); i8080.PC++; break;
				// RM
				case 0xf8: if (i8080.S) { RET(i8080); } break;
				// SPHL
				case 0xf9: i8080.SP = toWord(i8080.H, i8080.L); break;
				// JM
				case 0xfa: if (i8080.S) { JMP(i8080, nword); } break;
				// EI, opposite of DI
				case 0xfb: Console.WriteLine("Not implemented."); break;
				// CM
				case 0xfc: if (i8080.S) { CALL(i8080, nword); } break;
				// CPI, SUI but only change flags
				case 0xfe: Console.WriteLine("Not implemented."); break;
			}
			return i8080;
		}

		public static State Executor(State i8080, ushort romlength)
		{
			while (i8080.PC < romlength)
			{
				OpcodeHandler(i8080);
				i8080.PC++;
			}
			return i8080;
		}
	}
}