﻿namespace cs8080
{
	class ArithFlags
	{
		public bool Z = false, S = false, P = false, CY = false, AC = false;
		public static bool Parity(ushort ans) // left shift by 1 and add 0 if 0 and 1 if 1, then keep left shifting 1
		{
			byte oddcount = 0;
			for (byte i = 0; i < 8; i++)
			{
				oddcount += (byte)((ans >> i) & 1);
			}
			return (oddcount & 1) == 0;
		}

		public void SetAll()
		{
			Z = true;
			S = true;
			P = true;
			CY = true;
		}
		public void SetAllNC() // because sometimes we set carry flags in the instructions themselves
		{
			SetAll();
			CY = false;
		}
	}

	class ConditionCodes
	{
		public bool S = false, Z = false, P = false, CY = false, AC = false; // this is 1 8-bit register containing 5 1 bit flags
		public State Write(State i8080, ushort ans) // aux carry is implemented on a per instruction basis
		{
			Z = i8080.AF.Z & ((ans & 0xff) == 0);
			S = i8080.AF.S & Convert.ToBoolean((ans & 0xff) >> 7);
			P = i8080.AF.P & ArithFlags.Parity(ans);
			CY = i8080.AF.CY & (ans > 0xff);
			AC = i8080.AF.AC & (((ans & 0xf) + (i8080.A & 0xf)) > 0xf);
			return i8080;
		}
	}

	class State
	{
		public ArithFlags AF = new(); // arith flags
		public ConditionCodes CC = new(); // condition codes

		public const uint CLOCKSPEED = 2000000; // intel 8080 runs at 2Mhz
		public uint cycles; // cycle count of the prev instruction
		public uint totalcycles; // how many cycles have been executed

		public bool interrupt = false, interruptp = false; // bool for checking if interrupt pin is set, if interrupt is pending
		public ushort interruptop = 0x00; // interrupt opcode to use

		public byte B = 0x0, C = 0x0, D = 0x0, E = 0x0, H = 0x0, L = 0x0, A = 0x0; // 7 8-bit general purpose registers (can be used as 16 bit in pairs of 2) and a special accumulator register
		public ushort SP = 0x0, PC = 0x0; // stack pointer that keeps return address, program counter that points to the next instruction to execute

		public bool halt = false;

		public byte[] Mem { get; set; } // don't define size here as size should be defined on object initialisation
		public State(ushort memsize)
		{
			Mem = new byte[memsize];
		}
		public void MemWrite(ushort adr, byte val)
		{
			if (adr < FM.ROMl) // program crashes if it tries to write to somewhere that doesn't exist anyway
			{
				//Console.WriteLine($"Warning: Tried to write to {adr:x4}, which is in ROM");
			}
			Mem[adr] = val;
		}
		public void MemWrite(ushort adr, byte valhi, byte vallo)
		{
			MemWrite(adr, valhi);
			MemWrite((ushort)(adr + 1), vallo);
		}
		public void MemWrite(ushort adr, ushort val)
		{
			MemWrite((ushort)(adr + 1), (byte)(val >> 8));
			MemWrite(adr, (byte)val);
		}

		// virtual functions that are the default and can be overridden, so we can specify different i/o at runtime and not compile time, provided it exists
		virtual public byte PortIN(State i8080, byte nbyte)
		{
			Console.WriteLine("IN called");
			return A;
		}
		virtual public void PortOUT(State i8080, byte nbyte)
		{
			Console.WriteLine("OUT called");
		}
	}

	class Ops
	{
		public static ushort ToWord(byte hi/* left byte */, byte lo/* right byte */) // hi lo is big endian but whatever
		{
			return (ushort)(hi << 8 | lo);
		}

		public static byte SplitWordlo(ushort word)
		{
			return (byte)(word & 0xff);
		}

		public static byte SplitWordhi(ushort word)
		{
			return (byte)((word >> 8) & 0xff);
		}

		public static byte B2F(State i8080)
		{
			byte flags = 0;
			flags |= (byte)(Convert.ToByte(i8080.CC.S) << 7);
			flags |= (byte)(Convert.ToByte(i8080.CC.Z) << 6);
			flags |= (byte)(Convert.ToByte(i8080.CC.AC) << 4);
			flags |= (byte)(Convert.ToByte(i8080.CC.P) << 2);
			flags |= 1 << 1;
			flags |= (byte)(Convert.ToByte(i8080.CC.CY) << 0);
			return flags;
		}
	}

	class Emulate : Ops
	{
		private static State Arith(State i8080, ushort ans)
		{
			i8080.CC.Write(i8080, ans);
			i8080.A = (byte)ans;
			i8080.cycles = 4;
			return i8080;
		}

		private static State ArithAdd(State i8080, ushort ans)
		{
			i8080.CC.Write(i8080, (ushort)(ans + i8080.A));
			i8080.CC.AC = (((byte)ans & 0xf) + (i8080.A & 0xf)) > 0xf;
			i8080.A += (byte)ans;
			i8080.cycles = 4;
			return i8080;
		}

		private static State ArithByte(State i8080, ushort ans)
		{
			Arith(i8080, ans);
			i8080.PC++;
			i8080.cycles = 7;
			return i8080;
		}

		private static State DAD(State i8080, int addpair) // 32 bit int because we need 1 overflow bit for carry
		{
			int resultpair = addpair + ToWord(i8080.H, i8080.L);
			i8080.CC.CY = Convert.ToBoolean(resultpair >> 16); // right shift by 16 and if anything is left, carry bit is set to 1
			i8080.H = SplitWordhi((ushort)resultpair);
			i8080.L = SplitWordlo((ushort)resultpair);
			i8080.cycles = 10;
			return i8080;
		}

		private static State RET(State i8080)
		{
			i8080.PC = (ushort)(ToWord(i8080.Mem[i8080.SP + 1], i8080.Mem[i8080.SP]) + 2); // why is a +2 neccessary????
			i8080.SP += 2;
			i8080.cycles = 11; // most of the time
			return i8080;
		}

		private static State JMP(State i8080, ushort adr)
		{
			i8080.PC = (ushort)(adr - 1); // Executor always increments PC by 1 so undo that
			i8080.cycles = 10;
			return i8080;
		}

		private static State CALL(State i8080, ushort adr)
		{
			i8080.SP -= 2; // if stack pointer is at 0 it wont work
			i8080.MemWrite(i8080.SP, i8080.PC);
			JMP(i8080, adr);
			i8080.cycles = 17;
			return i8080;
		}

		public static State OpcodeHandler(State i8080, ushort ninst)
		{
		byte nbyte=0, nbyte2=0, leftbit, rightbit;
			ushort nword=0, regpair;
		try
		{
			nbyte = i8080.Mem[i8080.PC + 1];
			nbyte2 = i8080.Mem[i8080.PC + 2]; // leftbit rightbit are just variables that are used to hold values for a complex instruction
			nword = ToWord(i8080.Mem[i8080.PC + 2], i8080.Mem[i8080.PC + 1]); // use regpair because it made sense for a few things, and we can reuse this for other ushort var requirements
			}
			catch
			{
			}
			switch (ninst)
			{
				// NOP instruction, do nothing
				case 0x00 or 0x08 or 0x10 or 0x18 or 0x20 or 0x28 or 0x30 or 0x38: i8080.cycles = 4; break;
				// LXI instruction, load pc+1 and pc+2 into a register pair
				case 0x01: i8080.B = nbyte2; i8080.C = nbyte; i8080.PC += 2; i8080.cycles = 10; break;
				case 0x11: i8080.D = nbyte2; i8080.E = nbyte; i8080.PC += 2; i8080.cycles = 10; break;
				case 0x21: i8080.H = nbyte2; i8080.L = nbyte; i8080.PC += 2; i8080.cycles = 10; break;
				case 0x31: i8080.SP = nword; i8080.PC += 2; i8080.cycles = 10; break;
				// STAX instruction, store accumulator into mem, address indicated by register pair
				case 0x02: i8080.MemWrite(ToWord(i8080.B, i8080.C), i8080.A); i8080.cycles = 7; break;
				case 0x12: i8080.MemWrite(ToWord(i8080.D, i8080.E), i8080.A); i8080.cycles = 7; break;
				// INX instruction, join registers to word, then increment, then split word
				case 0x03: regpair = (ushort)(ToWord(i8080.B, i8080.C) + 1); i8080.B = SplitWordhi(regpair); i8080.C = SplitWordlo(regpair); i8080.cycles = 5; break;
				case 0x13: regpair = (ushort)(ToWord(i8080.D, i8080.E) + 1); i8080.D = SplitWordhi(regpair); i8080.E = SplitWordlo(regpair); i8080.cycles = 5; break;
				case 0x23: regpair = (ushort)(ToWord(i8080.H, i8080.L) + 1); i8080.H = SplitWordhi(regpair); i8080.L = SplitWordlo(regpair); i8080.cycles = 5; break;
				case 0x33: i8080.SP++; i8080.cycles = 5; break;
				// INR instruction, increment a register
				case 0x04: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.B + 1)); i8080.B++; i8080.CC.AC = (i8080.B & 0xf) == 0; i8080.cycles = 5; break;
				case 0x0c: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.C + 1)); i8080.C++; i8080.CC.AC = (i8080.C & 0xf) == 0; i8080.cycles = 5; break;
				case 0x14: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.D + 1)); i8080.D++; i8080.CC.AC = (i8080.D & 0xf) == 0; i8080.cycles = 5; break;
				case 0x1c: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.E + 1)); i8080.E++; i8080.CC.AC = (i8080.E & 0xf) == 0; i8080.cycles = 5; break;
				case 0x24: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.H + 1)); i8080.H++; i8080.CC.AC = (i8080.H & 0xf) == 0; i8080.cycles = 5; break;
				case 0x2c: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.L + 1)); i8080.L++; i8080.CC.AC = (i8080.L & 0xf) == 0; i8080.cycles = 5; break;
				case 0x34: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.Mem[ToWord(i8080.H, i8080.L)] + 1)); i8080.MemWrite(ToWord(i8080.H, i8080.L), (byte)(i8080.Mem[ToWord(i8080.H, i8080.L)] + 1)); i8080.CC.AC = ((i8080.Mem[ToWord(i8080.H, i8080.L)]+1) & 0xf) == 0; i8080.cycles = 10; break;
				case 0x3c: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.A + 1)); i8080.A++; i8080.CC.AC = (i8080.A & 0xf) == 0; i8080.cycles = 5; break;
				// DCR instruction, decrement a register
				case 0x05: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.B - 1)); i8080.B--; i8080.CC.AC = !((i8080.B & 0xf) == 0xf); i8080.cycles = 5; break;
				case 0x0d: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.C - 1)); i8080.C--; i8080.CC.AC = !((i8080.C & 0xf) == 0xf); i8080.cycles = 5; break;
				case 0x15: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.D - 1)); i8080.D--; i8080.CC.AC = !((i8080.D & 0xf) == 0xf); i8080.cycles = 5; break;
				case 0x1d: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.E - 1)); i8080.E--; i8080.CC.AC = !((i8080.E & 0xf) == 0xf); i8080.cycles = 5; break;
				case 0x25: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.H - 1)); i8080.H--; i8080.CC.AC = !((i8080.H & 0xf) == 0xf); i8080.cycles = 5; break;
				case 0x2d: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.L - 1)); i8080.L--; i8080.CC.AC = !((i8080.L & 0xf) == 0xf); i8080.cycles = 5; break;
				case 0x35: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.Mem[ToWord(i8080.H, i8080.L)] - 1)); i8080.MemWrite(ToWord(i8080.H, i8080.L), (byte)(i8080.Mem[ToWord(i8080.H, i8080.L)] - 1)); i8080.CC.AC = !(((i8080.Mem[ToWord(i8080.H, i8080.L)] - 1) & 0xf) == 0xf); i8080.cycles = 10; break;
				case 0x3d: i8080.AF.SetAllNC(); i8080.CC.Write(i8080, (ushort)(i8080.A - 1)); i8080.A--; i8080.CC.AC = !((i8080.A & 0xf) == 0xf); i8080.cycles = 5; break;
				// MVI instruction, loads register with next byte of the ROM
				case 0x06: i8080.B = nbyte; i8080.PC++; i8080.cycles = 7; break;
				case 0x0e: i8080.C = nbyte; i8080.PC++; i8080.cycles = 7; break;
				case 0x16: i8080.D = nbyte; i8080.PC++; i8080.cycles = 7; break;
				case 0x1e: i8080.E = nbyte; i8080.PC++; i8080.cycles = 7; break;
				case 0x26: i8080.H = nbyte; i8080.PC++; i8080.cycles = 7; break;
				case 0x2e: i8080.L = nbyte; i8080.PC++; i8080.cycles = 7; break;
				case 0x36: i8080.MemWrite(ToWord(i8080.H, i8080.L), nbyte); i8080.PC++; i8080.cycles = 10; break;
				case 0x3e: i8080.A = nbyte; i8080.PC++; i8080.cycles = 7; break;
				// RLC, rotates the accumulator left bitwise, carry is set to high order bit of A
				case 0x07:
					leftbit = (byte)(i8080.A >> 7);
					i8080.CC.CY = Convert.ToBoolean(leftbit);
					i8080.A = (byte)((byte)(i8080.A << 1) | leftbit);
					i8080.cycles = 4;
					break;
				// DAD, register pair is added to HL, also DAD() sets CY flag
				case 0x09: DAD(i8080, ToWord(i8080.B, i8080.C)); break; // cycles set in function
				case 0x19: DAD(i8080, ToWord(i8080.D, i8080.E)); break;
				case 0x29: DAD(i8080, ToWord(i8080.H, i8080.L)); break;
				case 0x39: DAD(i8080, i8080.SP); break;
				// LDAX, set accumulator to mem address of register pair BC or DE
				case 0x0a: i8080.A = i8080.Mem[ToWord(i8080.B, i8080.C)]; i8080.cycles = 7; break;
				case 0x1a: i8080.A = i8080.Mem[ToWord(i8080.D, i8080.E)]; i8080.cycles = 7; break;
				// DCX, decrement a register pair
				case 0x0b: regpair = ToWord(i8080.B, i8080.C); regpair--; i8080.B = SplitWordhi(regpair); i8080.C = SplitWordlo(regpair); i8080.cycles = 5; break;
				case 0x1b: regpair = ToWord(i8080.D, i8080.E); regpair--; i8080.D = SplitWordhi(regpair); i8080.E = SplitWordlo(regpair); i8080.cycles = 5; break;
				case 0x2b: regpair = ToWord(i8080.H, i8080.L); regpair--; i8080.H = SplitWordhi(regpair); i8080.L = SplitWordlo(regpair); i8080.cycles = 5; break;
				case 0x3b: i8080.SP--; i8080.cycles = 5; break;
				// RRC, rotates accumulator right, so RLC but right
				case 0x0f:
					rightbit = (byte)(i8080.A << 7);
					i8080.CC.CY = Convert.ToBoolean((byte)(rightbit >> 7));
					i8080.A = (byte)((byte)(i8080.A >> 1) | rightbit);
					i8080.cycles = 4;
					break;
				// RAL, RLC but carry bit is used for the lo bit and is set to hi bit
				case 0x17:
					leftbit = (byte)(i8080.A >> 7);
					i8080.A = (byte)((byte)(i8080.A << 1) | Convert.ToByte(i8080.CC.CY));
					i8080.CC.CY = Convert.ToBoolean(leftbit);
					i8080.cycles = 4;
					break;
				// RAR, RAL the other way
				case 0x1f:
					rightbit = (byte)(i8080.A << 7);
					i8080.A = (byte)((byte)(i8080.A >> 1) | Convert.ToByte(i8080.CC.CY));
					i8080.CC.CY = Convert.ToBoolean((byte)(rightbit >> 7));
					i8080.cycles = 4;
					break;
				// SHLD, L and H register stored at adr operand and adr operand + 1
				case 0x22:
					i8080.MemWrite(nword, ToWord(i8080.H, i8080.L));
					i8080.PC += 2;
					i8080.cycles = 16;
					break;
				// DAA, accumulator's 8 bit number is turned into 2 4-bit binary-coded-decimal digits (w h a t)
				case 0x27:
					leftbit = 0;
					rightbit = 0;
					if ((byte)(i8080.A & 0x0f) > 9 || i8080.CC.AC)
					{
						leftbit += 6;
					}
					if ((byte)(i8080.A >> 4) > 9 || i8080.CC.CY || ((byte)(i8080.A >> 4) >= 9 && (byte)(i8080.A & 0x0f) > 9))
					{
                        leftbit += 0x60;
						rightbit = 1;
					}
                    i8080.AF.SetAll();
                    i8080.CC.Write(i8080, (ushort)(leftbit + i8080.A));
                    i8080.A += leftbit;
					i8080.CC.CY = rightbit > 0;
					i8080.cycles = 4;
					break;
				// LHLD, SHLD but read instead of write
				case 0x2a:
					i8080.L = i8080.Mem[nword];
					i8080.H = i8080.Mem[nword + 1];
					i8080.PC += 2;
					i8080.cycles = 16;
					break;
				// CMA, bitflip accumulator
				case 0x2f: i8080.A = (byte)~i8080.A; i8080.cycles = 4; break;
				// STA, store accumulator at the adr operand
				case 0x32: i8080.MemWrite(nword, i8080.A); i8080.PC += 2; i8080.cycles = 13; break;
				// STC, set carry flag
				case 0x37: i8080.CC.CY = true; i8080.cycles = 4; break;
				// LDA, STA but adr operand's byte is written to the accumulator
				case 0x3a: i8080.A = i8080.Mem[nword]; i8080.PC += 2; i8080.cycles = 13; break;
				// CMC, carry flag inversed
				case 0x3f: i8080.CC.CY = !i8080.CC.CY; i8080.cycles = 4; break;
				// MOV, copy one register's value to another
				case 0x40: break; // B -> B
				case 0x41: i8080.B = i8080.C; i8080.cycles = 5; break;
				case 0x42: i8080.B = i8080.D; i8080.cycles = 5; break;
				case 0x43: i8080.B = i8080.E; i8080.cycles = 5; break;
				case 0x44: i8080.B = i8080.H; i8080.cycles = 5; break;
				case 0x45: i8080.B = i8080.L; i8080.cycles = 5; break;
				case 0x46: i8080.B = i8080.Mem[ToWord(i8080.H, i8080.L)]; i8080.cycles = 7; break;
				case 0x47: i8080.B = i8080.A; i8080.cycles = 5; break;
				case 0x48: i8080.C = i8080.B; i8080.cycles = 5; break;
				case 0x49: i8080.cycles = 5; break; // C -> C
				case 0x4a: i8080.C = i8080.D; i8080.cycles = 5; break;
				case 0x4b: i8080.C = i8080.E; i8080.cycles = 5; break;
				case 0x4c: i8080.C = i8080.H; i8080.cycles = 5; break;
				case 0x4d: i8080.C = i8080.L; i8080.cycles = 5; break;
				case 0x4e: i8080.C = i8080.Mem[ToWord(i8080.H, i8080.L)]; i8080.cycles = 7; break;
				case 0x4f: i8080.C = i8080.A; i8080.cycles = 5; break;
				case 0x50: i8080.D = i8080.B; i8080.cycles = 5; break;
				case 0x51: i8080.D = i8080.C; i8080.cycles = 5; break;
				case 0x52: i8080.cycles = 5; break; // D -> D
				case 0x53: i8080.D = i8080.E; i8080.cycles = 5; break;
				case 0x54: i8080.D = i8080.H; i8080.cycles = 5; break;
				case 0x55: i8080.D = i8080.L; i8080.cycles = 5; break;
				case 0x56: i8080.D = i8080.Mem[ToWord(i8080.H, i8080.L)]; i8080.cycles = 7; break;
				case 0x57: i8080.D = i8080.A; i8080.cycles = 5; break;
				case 0x58: i8080.E = i8080.B; i8080.cycles = 5; break;
				case 0x59: i8080.E = i8080.C; i8080.cycles = 5; break;
				case 0x5a: i8080.E = i8080.D; i8080.cycles = 5; break;
				case 0x5b: i8080.cycles = 5; break; // E -> E
				case 0x5c: i8080.E = i8080.H; i8080.cycles = 5; break;
				case 0x5d: i8080.E = i8080.L; i8080.cycles = 5; break;
				case 0x5e: i8080.E = i8080.Mem[ToWord(i8080.H, i8080.L)]; i8080.cycles = 7; break;
				case 0x5f: i8080.E = i8080.A; i8080.cycles = 5; break;
				case 0x60: i8080.H = i8080.B; i8080.cycles = 5; break;
				case 0x61: i8080.H = i8080.C; i8080.cycles = 5; break;
				case 0x62: i8080.H = i8080.D; i8080.cycles = 5; break;
				case 0x63: i8080.H = i8080.E; i8080.cycles = 5; break;
				case 0x64: i8080.cycles = 5; break; // H -> H
				case 0x65: i8080.H = i8080.L; i8080.cycles = 5; break;
				case 0x66: i8080.H = i8080.Mem[ToWord(i8080.H, i8080.L)]; i8080.cycles = 7; break;
				case 0x67: i8080.H = i8080.A; i8080.cycles = 5; break;
				case 0x68: i8080.L = i8080.B; i8080.cycles = 5; break;
				case 0x69: i8080.L = i8080.C; i8080.cycles = 5; break;
				case 0x6a: i8080.L = i8080.D; i8080.cycles = 5; break;
				case 0x6b: i8080.L = i8080.E; i8080.cycles = 5; break;
				case 0x6c: i8080.L = i8080.H; i8080.cycles = 5; break;
				case 0x6d: i8080.cycles = 5; break; // L -> L
				case 0x6e: i8080.L = i8080.Mem[ToWord(i8080.H, i8080.L)]; i8080.cycles = 7; break;
				case 0x6f: i8080.L = i8080.A; i8080.cycles = 5; break;
				case 0x70: i8080.MemWrite(ToWord(i8080.H, i8080.L), i8080.B); i8080.cycles = 7; break;
				case 0x71: i8080.MemWrite(ToWord(i8080.H, i8080.L), i8080.C); i8080.cycles = 7; break;
				case 0x72: i8080.MemWrite(ToWord(i8080.H, i8080.L), i8080.D); i8080.cycles = 7; break;
				case 0x73: i8080.MemWrite(ToWord(i8080.H, i8080.L), i8080.E); i8080.cycles = 7; break;
				case 0x74: i8080.MemWrite(ToWord(i8080.H, i8080.L), i8080.H); i8080.cycles = 7; break;
				case 0x75: i8080.MemWrite(ToWord(i8080.H, i8080.L), i8080.L); i8080.cycles = 7; break;
				case 0x76: Console.WriteLine("\n Halt requested, terminating execution..."); i8080.halt = true; i8080.cycles = 7; break; // HLT
				case 0x77: i8080.MemWrite(ToWord(i8080.H, i8080.L), i8080.A); i8080.cycles = 7; break;
				case 0x78: i8080.A = i8080.B; i8080.cycles = 5; break;
				case 0x79: i8080.A = i8080.C; i8080.cycles = 5; break;
				case 0x7a: i8080.A = i8080.D; i8080.cycles = 5; break;
				case 0x7b: i8080.A = i8080.E; i8080.cycles = 5; break;
				case 0x7c: i8080.A = i8080.H; i8080.cycles = 5; break;
				case 0x7d: i8080.A = i8080.L; i8080.cycles = 5; break;
				case 0x7e: i8080.A = i8080.Mem[ToWord(i8080.H, i8080.L)]; i8080.cycles = 7; break;
				case 0x7f: i8080.cycles = 5; break; // A -> A
				// ADD, add a register to the accumulator
				case 0x80: i8080.AF.SetAll(); ArithAdd(i8080, i8080.B); break;
				case 0x81: i8080.AF.SetAll(); ArithAdd(i8080, i8080.C); break;
				case 0x82: i8080.AF.SetAll(); ArithAdd(i8080, i8080.D); break;
				case 0x83: i8080.AF.SetAll(); ArithAdd(i8080, i8080.E); break;
				case 0x84: i8080.AF.SetAll(); ArithAdd(i8080, i8080.H); break;
				case 0x85: i8080.AF.SetAll(); ArithAdd(i8080, i8080.L); break;
				case 0x86: i8080.AF.SetAll(); ArithAdd(i8080, i8080.Mem[ToWord(i8080.H, i8080.L)]); i8080.cycles = 7; break;
				case 0x87: i8080.AF.SetAll(); ArithAdd(i8080, i8080.A); break;
				// ADC, ADD but with a carry flag
				case 0x88: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(i8080.B + Convert.ToByte(i8080.CC.CY))); break;
				case 0x89: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(i8080.C + Convert.ToByte(i8080.CC.CY))); break;
				case 0x8a: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(i8080.D + Convert.ToByte(i8080.CC.CY))); break;
				case 0x8b: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(i8080.E + Convert.ToByte(i8080.CC.CY))); break;
				case 0x8c: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(i8080.H + Convert.ToByte(i8080.CC.CY))); break;
				case 0x8d: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(i8080.L + Convert.ToByte(i8080.CC.CY))); break;
				case 0x8e: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(i8080.Mem[ToWord(i8080.H, i8080.L)] + Convert.ToByte(i8080.CC.CY))); i8080.cycles = 7; break;
				case 0x8f: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(i8080.A + Convert.ToByte(i8080.CC.CY))); break;
				// SUB, convert the register to 2's complement then add
				case 0x90: i8080.AF.SetAllNC(); ArithAdd(i8080, (ushort)(~i8080.B + 1)); break;
				case 0x91: i8080.AF.SetAllNC(); ArithAdd(i8080, (ushort)(~i8080.C + 1)); break;
				case 0x92: i8080.AF.SetAllNC(); ArithAdd(i8080, (ushort)(~i8080.D + 1)); break;
				case 0x93: i8080.AF.SetAllNC(); ArithAdd(i8080, (ushort)(~i8080.E + 1)); break;
				case 0x94: i8080.AF.SetAllNC(); ArithAdd(i8080, (ushort)(~i8080.H + 1)); break;
				case 0x95: i8080.AF.SetAllNC(); ArithAdd(i8080, (ushort)(~i8080.L + 1)); break;
				case 0x96: i8080.AF.SetAllNC(); ArithAdd(i8080, (ushort)(~i8080.Mem[ToWord(i8080.H, i8080.L)] + 1)); i8080.cycles = 7; break;
				case 0x97: i8080.AF.SetAllNC(); i8080.A = 0x0; i8080.CC.Write(i8080, i8080.A); i8080.cycles = 4; break; // subbing A is very simple
				// SBB, SUB but with a carry added to the register
				case 0x98: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(~(i8080.B + Convert.ToByte(i8080.CC.CY)) + 1)); break;
				case 0x99: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(~(i8080.C + Convert.ToByte(i8080.CC.CY)) + 1)); break;
				case 0x9a: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(~(i8080.D + Convert.ToByte(i8080.CC.CY)) + 1)); break;
				case 0x9b: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(~(i8080.E + Convert.ToByte(i8080.CC.CY)) + 1)); break;
				case 0x9c: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(~(i8080.H + Convert.ToByte(i8080.CC.CY)) + 1)); break;
				case 0x9d: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(~(i8080.L + Convert.ToByte(i8080.CC.CY)) + 1)); break;
				case 0x9e: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(~(i8080.Mem[ToWord(i8080.H, i8080.L)] + Convert.ToByte(i8080.CC.CY)) + 1)); i8080.cycles = 7; break;
				case 0x9f: i8080.AF.SetAll(); ArithAdd(i8080, (ushort)(~(i8080.A + Convert.ToByte(i8080.CC.CY)) + 1)); break;
				// ANA, accumulator logically AND'ed with specified reg
				case 0xa0: i8080.AF.SetAll(); i8080.AF.AC = true; Arith(i8080, (ushort)(i8080.A & i8080.B)); break;
				case 0xa1: i8080.AF.SetAll(); i8080.AF.AC = true; Arith(i8080, (ushort)(i8080.A & i8080.C)); break;
				case 0xa2: i8080.AF.SetAll(); i8080.AF.AC = true; Arith(i8080, (ushort)(i8080.A & i8080.D)); break;
				case 0xa3: i8080.AF.SetAll(); i8080.AF.AC = true; Arith(i8080, (ushort)(i8080.A & i8080.E)); break;
				case 0xa4: i8080.AF.SetAll(); i8080.AF.AC = true; Arith(i8080, (ushort)(i8080.A & i8080.H)); break;
				case 0xa5: i8080.AF.SetAll(); i8080.AF.AC = true; Arith(i8080, (ushort)(i8080.A & i8080.L)); break;
				case 0xa6: i8080.AF.SetAll(); i8080.AF.AC = true; Arith(i8080, (ushort)(i8080.A & i8080.Mem[ToWord(i8080.H, i8080.L)])); i8080.cycles = 7; break;
				case 0xa7: i8080.AF.SetAll(); i8080.CC.AC = true; Arith(i8080, (ushort)(i8080.A & i8080.A)); break;
				// XRA, accumulator XOR'd 
				case 0xa8: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A ^ i8080.B)); break;
				case 0xa9: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A ^ i8080.C)); break;
				case 0xaa: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A ^ i8080.D)); break;
				case 0xab: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A ^ i8080.E)); break;
				case 0xac: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A ^ i8080.H)); break;
				case 0xad: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A ^ i8080.L)); break;
				case 0xae: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A ^ i8080.Mem[ToWord(i8080.H, i8080.L)])); i8080.cycles = 7; break;
				case 0xaf: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A ^ i8080.A)); break;
				// ORA, accumulator OR'd
				case 0xb0: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A | i8080.B)); break;
				case 0xb1: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A | i8080.C)); break;
				case 0xb2: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A | i8080.D)); break;
				case 0xb3: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A | i8080.E)); break;
				case 0xb4: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A | i8080.H)); break;
				case 0xb5: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A | i8080.L)); break;
				case 0xb6: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A | i8080.Mem[ToWord(i8080.H, i8080.L)])); i8080.cycles = 7; break;
				case 0xb7: i8080.AF.SetAll(); Arith(i8080, (ushort)(i8080.A | i8080.A)); break;
				// CMP, i think it is like SUB but only setting conditional flags
				case 0xb8: i8080.AF.SetAllNC(); i8080.AF.AC = true; i8080.CC.Write(i8080, (ushort)(i8080.A + (~i8080.B) + 1)); i8080.cycles = 4; break;
				case 0xb9: i8080.AF.SetAllNC(); i8080.AF.AC = true; i8080.CC.Write(i8080, (ushort)(i8080.A + (~i8080.C) + 1)); i8080.cycles = 4; break;
				case 0xba: i8080.AF.SetAllNC(); i8080.AF.AC = true; i8080.CC.Write(i8080, (ushort)(i8080.A + (~i8080.D) + 1)); i8080.cycles = 4; break;
				case 0xbb: i8080.AF.SetAllNC(); i8080.AF.AC = true; i8080.CC.Write(i8080, (ushort)(i8080.A + (~i8080.E) + 1)); i8080.cycles = 4; break;
				case 0xbc: i8080.AF.SetAllNC(); i8080.AF.AC = true; i8080.CC.Write(i8080, (ushort)(i8080.A + (~i8080.H) + 1)); i8080.cycles = 4; break;
				case 0xbd: i8080.AF.SetAllNC(); i8080.AF.AC = true; i8080.CC.Write(i8080, (ushort)(i8080.A + (~i8080.L) + 1)); i8080.cycles = 4; break;
				case 0xbe: i8080.AF.SetAllNC(); i8080.AF.AC = true; i8080.CC.Write(i8080, (ushort)(i8080.A + (~i8080.Mem[ToWord(i8080.H, i8080.L)]) + 1)); i8080.cycles = 7; break;
				case 0xbf: i8080.AF.SetAllNC(); i8080.AF.AC = true; i8080.CC.Write(i8080, (ushort)(i8080.A + (~i8080.A ) + 1)); i8080.cycles = 4; break;
				// RNZ, return if not zero
				case 0xc0: if (!i8080.CC.Z) { RET(i8080); } else { i8080.cycles = 5; } break;
				// POP, pops the stack to a register pair
				case 0xc1: i8080.C = i8080.Mem[i8080.SP]; i8080.B = i8080.Mem[i8080.SP + 1]; i8080.SP += 2; i8080.cycles = 10; break;
				case 0xd1: i8080.E = i8080.Mem[i8080.SP]; i8080.D = i8080.Mem[i8080.SP + 1]; i8080.SP += 2; i8080.cycles = 10; break;
				case 0xe1: i8080.L = i8080.Mem[i8080.SP]; i8080.H = i8080.Mem[i8080.SP + 1]; i8080.SP += 2; i8080.cycles = 10; break;
				case 0xf1: // POP PSW, which is a wierd instruction
					leftbit = i8080.Mem[i8080.SP];
					i8080.CC.CY = (leftbit & 1) > 0;
					i8080.CC.P = ((leftbit >> 2) & 1) > 0;
					i8080.CC.AC = ((leftbit >> 4) & 1) > 0;
					i8080.CC.Z = ((leftbit >> 6) & 1) > 0;
					i8080.CC.S = ((leftbit >> 7) & 1) > 0;
					i8080.A = (byte)(i8080.Mem[i8080.SP + 1] -2);
					i8080.SP += 2;
					i8080.cycles = 10;
					break;
				// JNZ, jump if zero flag isnt set
				case 0xc2: if (!i8080.CC.Z) { JMP(i8080, nword); } else { i8080.PC += 2; } break;
				// JMP, jump to address
				case 0xc3: JMP(i8080, nword); break; // i don't think we need to increment PC by 2 since PC gets set to something else anyway
				// CNZ, call if zero flag isnt set
				case 0xc4: if (!i8080.CC.Z) { CALL(i8080, nword); } else { i8080.cycles = 11; i8080.PC += 2; } break;
				// PUSH, regpair pushed onto stack
				case 0xc5: i8080.SP -= 2; i8080.MemWrite(i8080.SP, i8080.C, i8080.B); i8080.cycles = 11; break;
				case 0xd5: i8080.SP -= 2; i8080.MemWrite(i8080.SP, i8080.E, i8080.D); i8080.cycles = 11; break;
				case 0xe5: i8080.SP -= 2; i8080.MemWrite(i8080.SP, i8080.L, i8080.H); i8080.cycles = 11; break;
				case 0xf5: i8080.SP -= 2; i8080.MemWrite(i8080.SP, B2F(i8080)); i8080.cycles = 11; break;
				// ADI, accumulator add next byte
				case 0xc6: i8080.AF.SetAll(); ArithAdd(i8080, nbyte); i8080.PC++; i8080.cycles = 7; break;
				// RST 0, 1, 2, 3, 4, 5, 6, 7 which returns to these set address
				case 0xc7: CALL(i8080, 0x00); i8080.cycles = 11; break;
				case 0xcf: CALL(i8080, 0x08); i8080.cycles = 11; break;
				case 0xd7: CALL(i8080, 0x10); i8080.cycles = 11; break;
				case 0xdf: CALL(i8080, 0x18); i8080.cycles = 11; break;
				case 0xe7: CALL(i8080, 0x20); i8080.cycles = 11; break;
				case 0xef: CALL(i8080, 0x28); i8080.cycles = 11; break;
				case 0xf7: CALL(i8080, 0x30); i8080.cycles = 11; break;
				case 0xff: CALL(i8080, 0x38); i8080.cycles = 11; break;
				// RZ, return if zero flag is set
				case 0xc8: if (i8080.CC.Z) { RET(i8080); } else { i8080.cycles = 5; } break;
				// RET, return op
				case 0xc9: RET(i8080); i8080.cycles--; break;
				// JZ, jump to addres if zero is set
				case 0xca: if (i8080.CC.Z) { JMP(i8080, nword); } else { i8080.PC += 2; } break; // these else statements make it so the data bytes are skipped
				// CZ, same as above but for call
				case 0xcc: if (i8080.CC.Z) { CALL(i8080, nword); } else { i8080.cycles = 11; i8080.PC += 2; } break;
				// CALL, call a function
				case 0xcd: CALL(i8080, nword); break;
				// ACI, ADI but with carry flag
				case 0xce: i8080.AF.SetAll(); i8080.AF.AC = true; ArithByte(i8080, (ushort)(i8080.A + (byte)(nbyte + Convert.ToByte(i8080.CC.CY)))); break;
				// RNC, return if carry isn't set
				case 0xd0: if (!i8080.CC.CY) { RET(i8080); } else { i8080.cycles = 5; } break;
				// JNC, jump if carry isn't set
				case 0xd2: if (!i8080.CC.CY) { i8080.PC = (ushort)(nword - 1); } else { i8080.PC += 2; } break; // if jump is failed then we still need to increment by 2
				// OUT, instruction to access output devices
				case 0xd3: i8080.PortOUT(i8080, nbyte); i8080.PC++; i8080.cycles = 10; break;
				// CNC, call if carry isn't set
				case 0xd4: if (!i8080.CC.CY) { CALL(i8080, nword); } else { i8080.cycles = 11; i8080.PC += 2; } break;
				// SUI, sub next byte from accumulator
				case 0xd6: i8080.AF.SetAll(); i8080.AF.AC = true; ArithByte(i8080, (ushort)(i8080.A - nbyte)); break;
				// RC, return if carry is set
				case 0xd8: if (i8080.CC.CY) { RET(i8080); } else { i8080.cycles = 5; } break;
				// JC, jump if carry is set
				case 0xda: if (i8080.CC.CY) { i8080.PC = (ushort)(nword - 1); } else { i8080.PC += 2; } break;
				// IN, accesses input devices
				case 0xdb: i8080.A = i8080.PortIN(i8080, nbyte); i8080.PC++; i8080.cycles = 10; break;
				// CC, call if carry is set
				case 0xdc: if (i8080.CC.CY) { CALL(i8080, nword); } else { i8080.cycles = 11; i8080.PC += 2; } break;
				// SBI, sui but with a carry
				case 0xde: i8080.AF.SetAll(); i8080.AF.AC = true; ArithByte(i8080, (ushort)(i8080.A - (byte)(nbyte + Convert.ToByte(i8080.CC.CY)))); break;
				// RPO, return if parity is odd
				case 0xe0: if (!i8080.CC.P) { RET(i8080); } else { i8080.cycles = 5; } break;
				// JPO, jump if parity is odd
				case 0xe2: if (!i8080.CC.P) { i8080.PC = (ushort)(nword - 1); } else { i8080.PC += 2; } break;
				// XTHL, swap HL with SP
				case 0xe3:
					leftbit = i8080.L; rightbit = i8080.H;
					i8080.L = i8080.Mem[i8080.SP];
					i8080.H = i8080.Mem[i8080.SP + 1];
					i8080.MemWrite(i8080.SP, leftbit, rightbit); // sets SP places to HL
					i8080.cycles = 18;
					break;
				// CPO, call if parity is odd
				case 0xe4: if (!i8080.CC.P) { CALL(i8080, nword); } else { i8080.cycles = 11; i8080.PC += 2; } break;
				// ANI, logical AND with accumulator and next byte
				case 0xe6: i8080.AF.SetAll(); i8080.AF.AC = true; ArithByte(i8080, (ushort)(i8080.A & nbyte)); break;
				// RPE, return if parity is even
				case 0xe8: if (i8080.CC.P) { RET(i8080); } else { i8080.cycles = 5; } break;
				// PCHL, set PC to HL
				case 0xe9: i8080.PC = (ushort)(ToWord(i8080.H, i8080.L) -1); i8080.cycles = 5; break; // remember that PC increments always so undo
				// JPE, jump if parity is even
				case 0xea: if (i8080.CC.P) { i8080.PC = (ushort)(nword - 1); } else { i8080.PC += 2; } break;
				// XCHG, swap DE and HL
				case 0xeb: (i8080.H, i8080.D) = (i8080.D, i8080.H); (i8080.L, i8080.E) = (i8080.E, i8080.L); i8080.cycles = 5; break;
				// CPE, call if parity is even
				case 0xec: if (i8080.CC.P) { CALL(i8080, nword); } else { i8080.cycles = 11; i8080.PC += 2; } break;
				// XRI, logical XOR with next byte and accumulator
				case 0xee: i8080.AF.SetAll(); ArithByte(i8080, (ushort)(i8080.A ^ nbyte)); break;
				// RP, return if sign isn't set
				case 0xf0: if (!i8080.CC.S) { RET(i8080); } else { i8080.cycles = 5; } break;
				// JP, jump if sign isn't set
				case 0xf2: if (!i8080.CC.S) { JMP(i8080, nword); } else { i8080.PC += 2; } break;
				// DI, disable interrupts
				case 0xf3: i8080.interrupt = false; i8080.cycles = 4; break;
				// CP, call if sign isn't set
				case 0xf4: if (!i8080.CC.S) { CALL(i8080, nword); } else { i8080.cycles = 11; i8080.PC += 2; } break;
				// ORI, logical OR with accumulator and next byte
				case 0xf6: i8080.AF.SetAll(); ArithByte(i8080, (ushort)(i8080.A | nbyte)); break;
				// RM, return if sign set
				case 0xf8: if (i8080.CC.S) { RET(i8080); } else { i8080.cycles = 5; } break;
				// SPHL, set stack pointer to HL 
				case 0xf9: i8080.SP = ToWord(i8080.H, i8080.L); i8080.cycles = 5; break;
				// JM, jump to addr if sign is set
				case 0xfa: if (i8080.CC.S) { JMP(i8080, nword); } else { i8080.PC += 2; } break;
				// EI, opposite of DI
				case 0xfb: i8080.interrupt = true; i8080.cycles = 4; break;
				// CM, call if sign is set
				case 0xfc: if (i8080.CC.S) { CALL(i8080, nword); } else { i8080.cycles = 11; i8080.PC += 2; } break;
				// CPI, SUI but only change flags
				case 0xfe: i8080.AF.SetAll(); i8080.CC.Write(i8080, (ushort)(i8080.A - nbyte)); i8080.CC.AC = ((nbyte & 0xf) + (i8080.A & 0xf)) > 0xf; i8080.PC++; i8080.cycles = 7; break;

				// UNDOCUMENTED INSTRUCTIONS:
				case 0xcb: JMP(i8080, nword); break;
				case 0xd9: RET(i8080); i8080.cycles--; break;
				case 0xdd or 0xed or 0xfd: CALL(i8080, nword); break;
			}
			i8080.PC++;
			i8080.totalcycles += i8080.cycles;
			return i8080;
		}

		public static State InterruptGen(State i8080, ushort op)
		{
			i8080.interruptp = true;
			i8080.interruptop = op;
			return i8080;
		}

		public static State Exec(State i8080)
		{
			/* if (i8080.interrupt)
			{
				i8080.interrupt = false;
				i8080.interruptp = false;
				OpcodeHandler(i8080, i8080.interruptop);
			}
			else if (!i8080.interrupt)
			{
				OpcodeHandler(i8080, i8080.Mem[i8080.PC]);
			}*/
			OpcodeHandler(i8080, i8080.Mem[i8080.PC]);
#if DEBUG
				Console.WriteLine($"PC: {i8080.PC:X4}, AF: {i8080.A:X2}{Ops.B2F(i8080):X2}, BC: {i8080.B:X2}{i8080.C:X2}, DE: {i8080.D:X2}{i8080.E:X2}, HL: {i8080.H:X2}{i8080.L:X2}, M: {i8080.Mem[ToWord(i8080.H, i8080.L)]}, SP: {i8080.SP:X4} - {Disassembler.OPlookup(i8080.Mem[i8080.PC], i8080.Mem[i8080.PC + 1], i8080.Mem[i8080.PC + 2])}");
				//FM.DumpAll(i8080, "dump");
#endif
			return i8080;
		}

		public static State Executor(State i8080)
		{
			while (i8080.PC < FM.ROMl || !i8080.halt)
			{
				Exec(i8080);
			}
			return i8080;
		}
	}
}
