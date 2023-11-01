using System.Collections;
using System.Runtime.InteropServices.Marshalling;

namespace cs8080
{
	class ConditionCodes
	{
		// this is 1 8-bit register containing 5 1 bit flags
		public bool S=false, Z=false, P=false, CY=false, AC=false;
	}

	class State : ConditionCodes
	{
		private const ushort MaxMem = 65535; // 64k of ram
		public byte B=0, C=0, D=0, E=0, H=0, L=0, M=0, A=0; // 7 8-bit general purpose registers (can be used as 16 bit in pairs of 2) and a special accumulator register
		public ushort SP=0, PC=0; // stack pointer that keeps return address, program counter that loads the next instruction to execute
		public byte[] mem8080 = new byte[MaxMem]; // 64k of ram as a byte array
	}

	class Instructions
	{
		private static byte nextByte(byte[] mem, ushort PC)
		{
			return mem[PC+1];
		}

		private static byte next2Byte(byte[] mem, ushort PC)
		{
			return mem[PC+2];
		}

		private static ushort toWord(byte hi/* left byte */, byte lo/* right byte */) // hi lo because little endian consistency
		{
			return (ushort)(hi << 8 | lo);
		}

		private static ushort nextWord(byte[] mem, ushort PC)
		{
			return toWord(mem[PC+2],mem[PC+1]);
		}

		protected static State NOP(State i8080)
		{
			return i8080;
		}

		protected static State LXI(State i8080)
		{
			switch (i8080.mem8080[i8080.PC]) // sets the registers to operate on
			{
				case 0x01: i8080.B = next2Byte(i8080.mem8080, i8080.PC); i8080.C = nextByte(i8080.mem8080, i8080.PC); break;
				case 0x11: i8080.D = next2Byte(i8080.mem8080, i8080.PC); i8080.E = nextByte(i8080.mem8080, i8080.PC); break;
				case 0x21: i8080.H = next2Byte(i8080.mem8080, i8080.PC); i8080.L = nextByte(i8080.mem8080, i8080.PC); break;
				case 0x31: i8080.SP = nextWord(i8080.mem8080, i8080.PC); break;
				default: break;
			}
         i8080.PC += 2;
      	return i8080;
		}

		protected static State STAX(State i8080)
		{
			switch (i8080.mem8080[i8080.PC])
			{
				case 0x02: i8080.mem8080[toWord(i8080.B,i8080.C)] = i8080.A; break;
				case 0x12: i8080.mem8080[toWord(i8080.D,i8080.E)] = i8080.A; break;
			}
			return i8080;
		}

		protected static State INX(State i8080)
		{
			byte reg1=0,reg2=0,indicator=0;
			switch (i8080.mem8080[i8080.PC])
			{
				case 0x01: indicator = 1; reg1 = i8080.B; reg2 = i8080.C; break;
				case 0x11: indicator = 2; reg1 = i8080.D; reg2 = i8080.E; break;
				case 0x21: indicator = 3; reg1 = i8080.H; reg2 = i8080.L; break;
				case 0x31: i8080.SP++; return i8080;
				default: return i8080;
			}
			ushort regpair = toWord(reg1, reg2);
			regpair++;
			reg1 = BitConverter.GetBytes(regpair)[0];
			reg2 = BitConverter.GetBytes(regpair)[1];
			switch (indicator)
			{
				case 1: i8080.B = reg1; i8080.C = reg2; break;
				case 2: i8080.D = reg1; i8080.E = reg2; break;
				case 3: i8080.H = reg1; i8080.L = reg2; break;
			}
			return i8080;
		}

		protected static State INR(State i8080)
		{
			switch (i8080.mem8080[i8080.PC])
			{
				case 0x04: i8080.B++; break;
				case 0x0c: i8080.C++; break;
				case 0x14: i8080.D++; break;
				case 0x1c: i8080.E++; break;
				case 0x24: i8080.H++; break;
				case 0x2c: i8080.L++; break;
				case 0x34: i8080.M++; break;
				case 0x3c: i8080.A++; break;
				default: break;
			}
			return i8080;
		}

		protected static State DCR(State i8080)
		{
			switch (i8080.mem8080[i8080.PC])
			{
				case 0x04: i8080.B--; break;
				case 0x0c: i8080.C--; break;
				case 0x14: i8080.D--; break;
				case 0x1c: i8080.E--; break;
				case 0x24: i8080.H--; break;
				case 0x2c: i8080.L--; break;
				case 0x34: i8080.M--; break;
				case 0x3c: i8080.A--; break;
				default: break;
			}
			return i8080;
		}

		protected static State MVI(State i8080)
		{
			byte byte2 = nextByte(i8080.mem8080, i8080.PC);
			switch (i8080.mem8080[i8080.PC])
			{
				case 0x06: i8080.B=byte2; break;
				case 0x0e: i8080.C=byte2; break;
				case 0x16: i8080.D=byte2; break;
				case 0x1e: i8080.E=byte2; break;
				case 0x26: i8080.H=byte2; break;
				case 0x2e: i8080.L=byte2; break;
				case 0x36: i8080.M=byte2; break;
				case 0x3e: i8080.A=byte2; break;
				default: break;
			}
			i8080.PC++;
			return i8080;
		}

		protected static State RLC(State i8080)
		{
			byte leftbit = (byte)(i8080.A >> 7);
			i8080.CY = Convert.ToBoolean(leftbit);
			i8080.A = (byte)((byte)(i8080.A << 1) | leftbit);
			return i8080;
		}

		protected static State JMP(State i8080)
		{
			i8080.PC = nextWord(i8080.mem8080, i8080.PC);
			i8080.PC--; // the program always increments PC by 1 so undo that
			return i8080;
		}
	}

	class Emulate8080 : Instructions
	{
      private static State OpcodeHandler(State i8080)
		{
			switch (i8080.mem8080[i8080.PC])
			{
				// NOP instruction
				case 0x00 or 0x08 or 0x10 or 0x18 or 0x20 or 0x28 or 0x30 or 0x38 or 0xcb or 0xd9 or 0xdd or 0xed or 0xfd: return NOP(i8080);
				// LXI instruction
				case 0x01 or 0x11 or 0x21 or 0x31: return LXI(i8080);
				// STAX instruction
				case 0x02 or 0x12: return STAX(i8080);
				// INX instruction
				case 0x03 or 0x13 or 0x23 or 0x33: return INX(i8080);
				// INR instruction
				case 0x04 or 0x0c or 0x14 or 0x1c or 0x24 or 0x2c or 0x34 or 0x3c: return INR(i8080);
				// DCR instruction
				case 0x05 or 0x0d or 0x15 or 0x1d or 0x25 or 0x2d or 0x35 or 0x3d: return DCR(i8080);
				// MVI instruction
				case 0x06 or 0x0e or 0x16 or 0x1e or 0x26 or 0x2e or 0x36 or 0x3e: return MVI(i8080);
				case 0x07: return RLC(i8080); // RLC
				// DAD instruction
				case 0x09 or 0x19 or 0x29 or 0x39: break;
				// LDAX instruction
				case 0x0a or 0x1a: break;
				// DCX instruction
				case 0x0b or 0x1b or 0x2b or 0x3b: break;
				case 0x0f: break; // RRC
				case 0x17: break; // RAL
				case 0x1f: break; // RAR
				case 0x22: break; // SHLD
				case 0x27: break; // DAA
				case 0x2a: break; // LHLD
				case 0x2f: break; // CMA
				case 0x32: break; // STA
				case 0x37: break; // STC
				case 0x3a: break; // LDA
				case 0x3f: break; // CMC
				// MOV
				case <= 0x75 and >= 0x40: break;
				case <= 0x7f and >= 0x77: break;
				case 0x76: break; // HLT
				// ADD
				case <= 0x87 and >= 0x80: break;
				// ADC
				case <= 0x8f and >= 0x89: break;
				// SUB
				case <= 0x97 and >= 0x90: break;
				// SBB
				case <= 0x9f and >= 0x99: break;
				// ANA
				case <= 0xa7 and >= 0xa0: break;
				// XRA
				case <= 0xaf and >= 0xa8: break;
				// ORA
				case <= 0xb7 and >= 0xb0: break;
				// CMP
				case <= 0xbf and >= 0xb8: break;
				case 0xc0: break; // RNZ
				// POP
				case 0xc1 or 0xd1 or 0xe1 or 0xf1: break;
				case 0xc2: break; // JNZ
				case 0xc3: return JMP(i8080); // JMP
				case 0xc4: break; // CNZ
				// PUSH
				case 0xc5 or 0xd5 or 0xe5 or 0xf5: break;
				case 0xc6: break; // ADI
				// RST
				case 0xc7 or 0xcf or 0xd7 or 0xdf or 0xe7 or 0xef or 0xf7 or 0xff: break;
				case 0xc8: break; // RZ
				case 0xc9: break; // RET
				case 0xca: break; // JZ
				case 0xcc: break; // CZ
				case 0xcd: break; // CALL
				case 0xce: break; // ACI
				case 0xd0: break; // RNC
				case 0xd2: break; // JNC
				case 0xd3: break; // OUT
				case 0xd4: break; // CNC
				case 0xd6: break; // SUI
				case 0xd8: break; // RC
				case 0xda: break; // JC
				case 0xdb: break; // IN
				case 0xdc: break; // CC
				case 0xde: break; // SBI
				case 0xe0: break; // RPO
				case 0xe2: break; // JPO
				case 0xe3: break; // XTHL
				case 0xe4: break; // CPO
				case 0xe6: break; // ANI
				case 0xe8: break; // RPE
				case 0xe9: break; // PCHL
				case 0xea: break; // JPE
				case 0xeb: break; // XCHG
				case 0xec: break; // CPE
				case 0xee: break; // XRI
				case 0xf0: break; // RP
				case 0xf2: break; // JP
				case 0xf3: break; // DI
				case 0xf4: break; // CP
				case 0xf6: break; // ORI
				case 0xf8: break; // RM
				case 0xf9: break; // SPHL
				case 0xfa: break; // JM
				case 0xfb: break; // EI
				case 0xfc: break; // CM
				case 0xfe: break; // CPI
			}
			Console.WriteLine(" Not implemented.");
			return i8080;
		}

		public static State Executor(State i8080)
		{
			//int i = 0;
			while (i8080.PC < i8080.mem8080.Length)
			{
				//Console.Write($"{i:x2} {i8080.mem8080[i8080.PC]:x2}"); i++; // memory demp to the terminal with each instruction i guess
				OpcodeHandler(i8080);
				i8080.PC++;
			}
			return i8080;
		}
	}
}
