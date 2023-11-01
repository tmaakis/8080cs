namespace cs8080
{
	class ArithFlags
	{
		public bool ZeroF = false, SignF = false, ParityF = false, CarryF = false, AuxCarryF = false;
		protected bool Zero(ushort ans)
		{
			return (ans & 0xff) == 0;
		}

		protected bool Sign(ushort ans)
		{
			return Convert.ToBoolean((ans & 0xff) >> 7);
		}

		protected bool Parity(ushort ans) // left shift by 1 and add 0 if 0 and 1 if 1, then keep left shifting 1
		{
			byte oddcount = 0;
			for (byte i = 1; i <= 8; i++)
			{
				ans = (ushort)(ans << 1);
				oddcount += Ops.splitWordhi(ans);
				ans = Ops.splitWordlo(ans);
			}
			return oddcount % 2 == 0;
		}

		protected bool Carry(ushort ans)
		{
			return ans > 0xff;
		}

		protected bool AuxCarry(ushort ans)
		{
			return (byte)((byte)(ans & 0xff) & 0b00011111) > 0xf;
		}

		public void SetAll()
		{
			ZeroF = true;
			SignF = true;
			ParityF = true;
			CarryF = true;
			AuxCarryF = true;
		}

		public void SetAllNC() // because sometimes we set carry flags in the instructions themselves
		{
			SetAll();
			CarryF = false;
		}

		public void SetC()
		{
			CarryF = true;
		}

		public void SetZSP()
		{
			ZeroF = true;
			SignF = true;
			ParityF = true;
		}
	}

	class ConditionCodes : ArithFlags
	{
		// this is 1 8-bit register containing 5 1 bit flags
		public bool S = false, Z = false, P = false, CY = false, AC = false;
		public State Write(State i8080, ArithFlags flags, ushort ans)
		{
			i8080.Z = flags.ZeroF & Zero(ans);
			i8080.S = flags.SignF & Sign(ans);
			i8080.P = flags.ParityF & Parity(ans);
			i8080.CY = flags.CarryF & Carry(ans);
			i8080.AC = flags.AuxCarryF & AuxCarry(ans);
			return i8080;
		}
	}

	class State : ConditionCodes
	{
		private const ushort MaxMem = 65535; // 64k of ram
		public byte B = 0x0, C = 0x0, D = 0, E = 0, H = 0x0, L = 0x0, A = 0x0; // 7 8-bit general purpose registers (can be used as 16 bit in pairs of 2) and a special accumulator register
		public ushort SP = 0, PC = 0x0; // stack pointer that keeps return address, program counter that loads the next instruction to execute
		public bool interrupt=true;
		public byte[] mem8080 = new byte[MaxMem]; // 64k of ram as a byte array
	}

	class Ops
	{
		public static byte nextByte(byte[] mem, ushort PC)
		{
			return mem[PC + 1];
		}

		public static byte next2Byte(byte[] mem, ushort PC)
		{
			return mem[PC + 2];
		}

		public static ushort toWord(byte hi/* left byte */, byte lo/* right byte */) // hi lo is big endian but whatever
		{
			return (ushort)(hi << 8 | lo);
		}

		public static ushort nextWord(byte[] mem, ushort PC)
		{
			return toWord(mem[PC + 2], mem[PC + 1]);
		}

		public static byte splitWordlo(ushort word)
		{
			return (byte)(word & 0xff);
		}

		public static byte splitWordhi(ushort word)
		{
			return (byte)((word >> 8) & 0xff);
		}

		public static byte B2F(State i8080)
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
		protected static State INR(State i8080, byte reg)
		{
			ArithFlags flags = new();
			flags.SetAllNC();
			i8080.Write(i8080, flags, (ushort)(reg + 1));
			return i8080;
		}

		protected static State DCR(State i8080, byte reg)
		{
			ArithFlags flags = new();
			flags.SetAllNC();
			i8080.Write(i8080, flags, (ushort)(reg - 1));
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

		protected static State RET(State i8080)
		{
			i8080.PC = toWord(i8080.mem8080[i8080.SP + 1], i8080.mem8080[i8080.SP]);
			i8080.SP += 2;
			return i8080;
		}

		protected static State JMP(State i8080, ushort adr)
		{
			i8080.PC = (ushort)(adr-1); // Executor always increments PC by 1 so undo that
			// if jump is failed then we still need to increment by 2
			return i8080;
		}

		protected static State CALL(State i8080, ushort adr)
		{
			i8080.SP -= 2; // if stack pointer is at 0 it wont work
			i8080.mem8080[i8080.SP + 1] = splitWordhi(i8080.PC);
			i8080.mem8080[i8080.SP] = splitWordlo(i8080.PC);
			if (i8080.C == 9)
			{
				ushort addr = (ushort)((i8080.D << 8) | i8080.E);
				do
				{
					Console.Write((char)i8080.mem8080[addr++]);
				} while ((char)i8080.mem8080[addr] != '$');
				Environment.Exit(1);
			}
			else if (i8080.C == 0x0002)
			{
				Console.WriteLine((char)i8080.E);
			}
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
			ArithFlags flags = new();
			switch (i8080.mem8080[i8080.PC])
			{
				// NOP instruction, do nothing
				case 0x00 or 0x08 or 0x10 or 0x18 or 0x20 or 0x28 or 0x30 or 0x38: break;
				// LXI instruction, load pc+1 and pc+2 into a register pair
				case 0x01: i8080.B = nbyte2; i8080.C = nbyte; i8080.PC += 2; break;
				case 0x11: i8080.D = nbyte2; i8080.E = nbyte; i8080.PC += 2; break;
				case 0x21: i8080.H = nbyte2; i8080.L = nbyte; i8080.PC += 2; break;
				case 0x31: i8080.SP = nword; i8080.PC += 2; break;
				// STAX instruction, store accumulator into mem, address indicated by register pair
				case 0x02: i8080.mem8080[toWord(i8080.B, i8080.C)] = i8080.A; break;
				case 0x12: i8080.mem8080[toWord(i8080.D, i8080.E)] = i8080.A; break;
				// INX instruction, join registers to word, then increment, then split word
				case 0x03: regpair = (ushort)(toWord(i8080.B, i8080.C)+1); i8080.B = splitWordhi(regpair); i8080.C = splitWordlo(regpair); break;
				case 0x13: regpair = (ushort)(toWord(i8080.D, i8080.E)+1); i8080.D = splitWordhi(regpair); i8080.E = splitWordlo(regpair); break;
				case 0x23: regpair = (ushort)(toWord(i8080.H, i8080.L)+1); i8080.H = splitWordhi(regpair); i8080.L = splitWordlo(regpair); break;
				case 0x33: i8080.SP++; break;
				// INR instruction, increment a register
				case 0x04: INR(i8080, i8080.B); i8080.B++; break;
				case 0x0c: INR(i8080, i8080.C); i8080.C++; break;
				case 0x14: INR(i8080, i8080.D); i8080.D++; break;
				case 0x1c: INR(i8080, i8080.E); i8080.E++; break;
				case 0x24: INR(i8080, i8080.H); i8080.H++; break;
				case 0x2c: INR(i8080, i8080.L); i8080.L++; break;
				case 0x34: INR(i8080, ahl); i8080.mem8080[toWord(i8080.H, i8080.L)] = (byte)(ahl + 1); break;
				case 0x3c: INR(i8080, i8080.A); i8080.A++; break;
				// DCR instruction, decrement a register
				case 0x05: DCR(i8080, i8080.B); i8080.B--; break;
				case 0x0d: DCR(i8080, i8080.C); i8080.C--; break;
				case 0x15: DCR(i8080, i8080.D); i8080.D--; break;
				case 0x1d: DCR(i8080, i8080.E); i8080.E--; break;
				case 0x25: DCR(i8080, i8080.H); i8080.H--; break;
				case 0x2d: DCR(i8080, i8080.L); i8080.L--; break;
				case 0x35: DCR(i8080, ahl); i8080.mem8080[toWord(i8080.H, i8080.L)] = (byte)(ahl - 1); break;
				case 0x3d: DCR(i8080, i8080.A); i8080.A--; break;
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
				// DAD, register pair is added to HL, also DAD() sets CY flag
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
				// DAA, accumulator's 8 bit number is turned into 2 4-bit binary-coded-decimal digits (w h a t) (SetAllNC)
				case 0x27:
					if ((byte)(i8080.A & 0x0f) > 0x09 || i8080.AC)
					{
						i8080.A += 6;
						i8080.AC = (byte)(i8080.A & 0x0f) + 0x06 > 0x10;
					}
					if ((byte)(i8080.A & 0xf0) > 0x90 || i8080.CY)
					{
						regpair = (ushort)(i8080.A + 0x60);
						flags.SetZSP(); flags.SetC();
						i8080.Write(i8080, flags, regpair);
						i8080.A = (byte)(regpair & 0xff);
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
				case 0x40: break; // B -> B
				case 0x41: i8080.B = i8080.C; break;
				case 0x42: i8080.B = i8080.D; break;
				case 0x43: i8080.B = i8080.E; break;
				case 0x44: i8080.B = i8080.H; break;
				case 0x45: i8080.B = i8080.L; break;
				case 0x46: i8080.B = ahl; break;
				case 0x47: i8080.B = i8080.A; break;
				case 0x48: i8080.C = i8080.B; break;
				case 0x49: break; // C -> C
				case 0x4a: i8080.C = i8080.D; break;
				case 0x4b: i8080.C = i8080.E; break;
				case 0x4c: i8080.C = i8080.H; break;
				case 0x4d: i8080.C = i8080.L; break;
				case 0x4e: i8080.C = ahl; break;
				case 0x4f: i8080.C = i8080.A; break;
				case 0x50: i8080.D = i8080.B; break;
				case 0x51: i8080.D = i8080.C; break;
				case 0x52: break; // D -> D
				case 0x53: i8080.D = i8080.E; break;
				case 0x54: i8080.D = i8080.H; break;
				case 0x55: i8080.D = i8080.L; break;
				case 0x56: i8080.D = ahl; break;
				case 0x57: i8080.D = i8080.A; break;
				case 0x58: i8080.E = i8080.B; break;
				case 0x59: i8080.E = i8080.C; break;
				case 0x5a: i8080.E = i8080.D; break;
				case 0x5b: break; // E -> E
				case 0x5c: i8080.E = i8080.H; break;
				case 0x5d: i8080.E = i8080.L; break;
				case 0x5e: i8080.E = ahl; break;
				case 0x5f: i8080.H = i8080.A; break;
				case 0x60: i8080.H = i8080.B; break;
				case 0x61: i8080.H = i8080.C; break;
				case 0x62: i8080.H = i8080.D; break;
				case 0x63: i8080.H = i8080.E; break;
				case 0x64: break; // H -> H
				case 0x65: i8080.H = i8080.L; break;
				case 0x66: i8080.H = ahl; break;
				case 0x67: i8080.L = i8080.A; break;
				case 0x68: i8080.L = i8080.B; break;
				case 0x69: i8080.L = i8080.C; break;
				case 0x6a: i8080.L = i8080.D; break;
				case 0x6b: i8080.L = i8080.E; break;
				case 0x6c: i8080.L = i8080.H; break;
				case 0x6d: break; // L -> L
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
				case 0x7f: break; // A -> A
				// ADD, add a register to the accumulator
				case 0x80: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += i8080.B); break;
				case 0x81: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += i8080.C); break;
				case 0x82: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += i8080.D); break;
				case 0x83: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += i8080.E); break;
				case 0x84: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += i8080.H); break;
				case 0x85: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += i8080.L); break;
				case 0x86: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += ahl); break;
				case 0x87: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += i8080.A); break;
				// ADC, ADD but with a carry flag
				case 0x88: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += (byte)(i8080.B + Convert.ToByte(i8080.CY))); break;
				case 0x89: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += (byte)(i8080.C + Convert.ToByte(i8080.CY))); break;
				case 0x8a: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += (byte)(i8080.D + Convert.ToByte(i8080.CY))); break;
				case 0x8b: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += (byte)(i8080.E + Convert.ToByte(i8080.CY))); break;
				case 0x8c: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += (byte)(i8080.H + Convert.ToByte(i8080.CY))); break;
				case 0x8d: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += (byte)(i8080.L + Convert.ToByte(i8080.CY))); break;
				case 0x8e: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += (byte)(ahl + Convert.ToByte(i8080.CY))); break;
				case 0x8f: flags.SetAll(); i8080.Write(i8080, flags, i8080.A += (byte)(i8080.A + Convert.ToByte(i8080.CY))); break;
				// SUB, convert the register to 2's complement then add
				case 0x90: flags.SetAll(); regpair = (ushort)(i8080.A + (~i8080.B & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.B = (byte)regpair; break;
				case 0x91: flags.SetAll(); regpair = (ushort)(i8080.A + (~i8080.C & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.C = (byte)regpair; break;
				case 0x92: flags.SetAll(); regpair = (ushort)(i8080.A + (~i8080.D & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.D = (byte)regpair; break;
				case 0x93: flags.SetAll(); regpair = (ushort)(i8080.A + (~i8080.E & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.E = (byte)regpair; break;
				case 0x94: flags.SetAll(); regpair = (ushort)(i8080.A + (~i8080.H & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.H = (byte)regpair; break;
				case 0x95: flags.SetAll(); regpair = (ushort)(i8080.A + (~i8080.L & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.L = (byte)regpair; break;
				case 0x96: flags.SetAll(); regpair = (ushort)(i8080.A + (~ahl & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.mem8080[toWord(i8080.H, i8080.L)] = (byte)regpair; break;
				case 0x97: flags.SetAll(); i8080.A = 0x0; i8080.Write(i8080, flags, i8080.A); break; // subbing A is very simple
				// SBB, SUB but with a carry added to the register
				case 0x98: flags.SetAll(); regpair = (ushort)(i8080.A + (~(i8080.B + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.B = (byte)regpair; break;
				case 0x99: flags.SetAll(); regpair = (ushort)(i8080.A + (~(i8080.C + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.C = (byte)regpair; break;
				case 0x9a: flags.SetAll(); regpair = (ushort)(i8080.A + (~(i8080.D + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.D = (byte)regpair; break;
				case 0x9b: flags.SetAll(); regpair = (ushort)(i8080.A + (~(i8080.E + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.E = (byte)regpair; break;
				case 0x9c: flags.SetAll(); regpair = (ushort)(i8080.A + (~(i8080.H + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.H = (byte)regpair; break;
				case 0x9d: flags.SetAll(); regpair = (ushort)(i8080.A + (~(i8080.L + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.L = (byte)regpair; break;
				case 0x9e: flags.SetAll(); regpair = (ushort)(i8080.A + (~(ahl + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.mem8080[toWord(i8080.H, i8080.L)] = (byte)regpair; break;
				case 0x9f: flags.SetAll(); regpair = (ushort)(i8080.A + (~(i8080.A + Convert.ToByte(i8080.CY)) & 0xff) + 1); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				// ANA, accumulator logically AND'ed with specified reg
				case 0xa0: flags.SetAll(); regpair = (ushort)(i8080.A & i8080.B); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xa1: flags.SetAll(); regpair = (ushort)(i8080.A & i8080.C); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xa2: flags.SetAll(); regpair = (ushort)(i8080.A & i8080.D); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xa3: flags.SetAll(); regpair = (ushort)(i8080.A & i8080.E); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xa4: flags.SetAll(); regpair = (ushort)(i8080.A & i8080.H); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xa5: flags.SetAll(); regpair = (ushort)(i8080.A & i8080.L); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xa6: flags.SetAll(); regpair = (ushort)(i8080.A & ahl); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xa7: flags.SetAll(); regpair = (ushort)(i8080.A & i8080.A); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				// XRA, accumulator XOR'd 
				case 0xa8: flags.SetAll(); regpair = (ushort)(i8080.A ^ i8080.B); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xa9: flags.SetAll(); regpair = (ushort)(i8080.A ^ i8080.C); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xaa: flags.SetAll(); regpair = (ushort)(i8080.A ^ i8080.D); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xab: flags.SetAll(); regpair = (ushort)(i8080.A ^ i8080.E); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xac: flags.SetAll(); regpair = (ushort)(i8080.A ^ i8080.H); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xad: flags.SetAll(); regpair = (ushort)(i8080.A ^ i8080.L); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xae: flags.SetAll(); regpair = (ushort)(i8080.A ^ ahl); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xaf: flags.SetAll(); regpair = (ushort)(i8080.A ^ i8080.A); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				// ORA, accumulator OR'd
				case 0xb0: flags.SetAll(); regpair = (ushort)(i8080.A | i8080.B); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xb1: flags.SetAll(); regpair = (ushort)(i8080.A | i8080.C); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xb2: flags.SetAll(); regpair = (ushort)(i8080.A | i8080.D); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xb3: flags.SetAll(); regpair = (ushort)(i8080.A | i8080.E); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xb4: flags.SetAll(); regpair = (ushort)(i8080.A | i8080.H); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xb5: flags.SetAll(); regpair = (ushort)(i8080.A | i8080.L); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xb6: flags.SetAll(); regpair = (ushort)(i8080.A | ahl); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				case 0xb7: flags.SetAll(); regpair = (ushort)(i8080.A | i8080.A); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; break;
				// CMP, i think it is like SUB but only setting conditional flags
				case 0xb8: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A + (~i8080.B & 0xff) + 1)); break;
				case 0xb9: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A + (~i8080.C & 0xff) + 1)); break;
				case 0xba: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A + (~i8080.D & 0xff) + 1)); break;
				case 0xbb: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A + (~i8080.E & 0xff) + 1)); break;
				case 0xbc: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A + (~i8080.H & 0xff) + 1)); break;
				case 0xbd: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A + (~i8080.L & 0xff) + 1)); break;
				case 0xbe: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A + (~ahl & 0xff) + 1)); break;
				case 0xbf: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A + (~i8080.A & 0xff) + 1)); break;
				// RNZ, return if not zero
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
				case 0xc2: if (!i8080.Z) { JMP(i8080, nword); } else { i8080.PC += 2; } break;
				// JMP, jump to address
				case 0xc3: JMP(i8080, nword); break; // i don't think we need to increment PC by 2 since PC gets set to something else anyway
				// CNZ
				case 0xc4: if (!i8080.Z) { CALL(i8080, nword); } else { i8080.PC += 2; } break;
				// PUSH
				case 0xc5: i8080.mem8080[i8080.SP - 2] = i8080.C; i8080.mem8080[i8080.SP - 1] = i8080.B; i8080.SP -= 2; break;
				case 0xd5: i8080.mem8080[i8080.SP - 2] = i8080.E; i8080.mem8080[i8080.SP - 1] = i8080.D; i8080.SP -= 2; break;
				case 0xe5: i8080.mem8080[i8080.SP - 2] = i8080.L; i8080.mem8080[i8080.SP - 1] = i8080.H; i8080.SP -= 2; break;
				case 0xf5: i8080.mem8080[i8080.SP - 2] = B2F(i8080); i8080.SP -= 2; break;
				// ADI
				case 0xc6: flags.SetAll(); regpair = (ushort)(i8080.A + nbyte); i8080.Write(i8080, flags,regpair); i8080.A = (byte)(i8080.A + nbyte); i8080.PC++; break;
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
				case 0xca: if (i8080.Z) { JMP(i8080, nword); } else { i8080.PC += 2; } break;
				// CZ
				case 0xcc: if (i8080.Z) { CALL(i8080, nword); } else { i8080.PC += 2; } break;
				// CALL
				case 0xcd: CALL(i8080, nword); break;
				// ACI
				case 0xce: flags.SetAll(); regpair = (ushort)(i8080.A + (byte)(nbyte + Convert.ToByte(i8080.CY))); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; i8080.PC++; break;
				// RNC
				case 0xd0: if (!i8080.CY) { RET(i8080); } break;
				// JNC
				case 0xd2: if (!i8080.CY) { i8080.PC = (ushort)(nword - 1); } else { i8080.PC += 2; } break;
				// OUT
				case 0xd3: Console.WriteLine("I/O not implemented."); break;
				// CNC
				case 0xd4: if (!i8080.CY) { CALL(i8080, nword); } else { i8080.PC += 2; } break;
				// SUI
				case 0xd6: flags.SetAll(); regpair = (ushort)(i8080.A - nbyte); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; i8080.PC++; break;
				// RC
				case 0xd8: if (i8080.CY) { RET(i8080); } break;
				// JC
				case 0xda: if (i8080.CY) { i8080.PC = (ushort)(nword - 1); } else { i8080.PC += 2; } break;
				// IN
				case 0xdb: Console.WriteLine("I/O not implemented."); break;
				// CC
				case 0xdc: if (i8080.CY) { CALL(i8080, nword); } else { i8080.PC += 2; } break;
				// SBI
				case 0xde: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A - (byte)(nbyte + Convert.ToByte(i8080.CY)))); i8080.A -= (byte)(nbyte + Convert.ToByte(i8080.CY)); i8080.PC++; break;
				// RPO
				case 0xe0: if (!i8080.P) { RET(i8080); } break;
				// JPO
				case 0xe2: if (!i8080.P) { i8080.PC = (ushort)(nword - 1); } else { i8080.PC += 2; } break;
				// XTHL
				case 0xe3: (i8080.L, i8080.mem8080[i8080.SP]) = (i8080.mem8080[i8080.SP], i8080.L); (i8080.H, i8080.mem8080[i8080.SP+1]) = (i8080.mem8080[i8080.SP+1], i8080.H); break;
				// CPO
				case 0xe4: if (!i8080.P) { CALL(i8080, nword); } else { i8080.PC += 2; } break;
				// ANI
				case 0xe6: flags.SetAll(); regpair = (ushort)(i8080.A & nbyte); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; i8080.PC++; break;
				// RPE
				case 0xe8: if (i8080.P) { RET(i8080); } break;
				// PCHL
				case 0xe9: i8080.PC = toWord(i8080.H, i8080.L); break;
				// JPE
				case 0xea: if (i8080.P) { i8080.PC = (ushort)(nword - 1); } else { i8080.PC += 2; } break;
				// XCHG
				case 0xeb: (i8080.H, i8080.D) = (i8080.D, i8080.H); (i8080.L, i8080.E) = (i8080.E, i8080.L); break;
				// CPE
				case 0xec: if (i8080.P) { CALL(i8080, nword); } else { i8080.PC += 2; } break;
				// XRI
				case 0xee: flags.SetAll(); regpair = (ushort)(i8080.A ^ nbyte); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; i8080.PC++; break;
				// RP
				case 0xf0: if (!i8080.S) { RET(i8080); } break;
				// JP
				case 0xf2: if (!i8080.S) { JMP(i8080, nword); } else { i8080.PC += 2; } break;
				// DI, disable interrupts
				case 0xf3: i8080.interrupt = false; break;
				// CP
				case 0xf4: if (!i8080.S) { CALL(i8080, nword); } else { i8080.PC += 2; } break;
				// ORI
				case 0xf6: flags.SetAll(); regpair = (ushort)(i8080.A | nbyte); i8080.Write(i8080, flags, regpair); i8080.A = (byte)regpair; i8080.PC++; break;
				// RM
				case 0xf8: if (i8080.S) { RET(i8080); } break;
				// SPHL
				case 0xf9: i8080.SP = toWord(i8080.H, i8080.L); break;
				// JM
				case 0xfa: if (i8080.S) { JMP(i8080, nword); } else { i8080.PC += 2; } break;
				// EI, opposite of DI
				case 0xfb: i8080.interrupt = true; break;
				// CM
				case 0xfc: if (i8080.S) { CALL(i8080, nword); } else { i8080.PC += 2; } break;
				// CPI, SUI but only change flags
				case 0xfe: flags.SetAll(); i8080.Write(i8080, flags, (ushort)(i8080.A - nbyte)); i8080.PC++; break;

				// UNDOCUMENTED INSTRUCTIONS:
				case 0xcb: JMP(i8080, nword); break;
				case 0xd9: RET(i8080); break;
				case 0xdd or 0xed or 0xfd: CALL(i8080, nword); break;
			}
			return i8080;
		}

		public static State Executor(State i8080, ushort romlength)
		{
			while (i8080.PC < romlength)
			{
				Console.Write($"{i8080.PC:x4} {Disassembler.OPlookup(i8080.mem8080[i8080.PC], i8080.mem8080[i8080.PC + 1], i8080.mem8080[i8080.PC + 2])} ");
				OpcodeHandler(i8080);
				Console.Write($"A={i8080.A} \n");
				i8080.PC++;
				FM.StateDump(i8080, "statedump.txt");
				//Thread.Sleep(100);
			}
			return i8080;
		}
	}
}
