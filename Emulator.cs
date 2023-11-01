﻿using System.Net.Sockets;
using System.Runtime.CompilerServices;

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
			return oddcount % 2 == 0;
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
				// ANA
				case <= 0xa7 and >= 0xa0: break;
				// XRA
				case <= 0xaf and >= 0xa8: break;
				// ORA
				case <= 0xb7 and >= 0xb0: break;
				// CMP
				case <= 0xbf and >= 0xb8: break;
				// RNZ
				case 0xc0: break;
				// POP
				case 0xc1 or 0xd1 or 0xe1 or 0xf1: break;
				case 0xc2: break; // JNZ
				// JMP, jump to address
				case 0xc3: i8080.PC = nextWord(i8080.mem8080, i8080.PC); i8080.PC--; break; // Executor always increments PC by 1 so undo that 
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
			Console.WriteLine($"{i8080.mem8080[i8080.PC]:x2} Not implemented.");
			return i8080;
		}

		public static State Executor(State i8080, ushort romlength)
		{
			//int i = 0;
			while (i8080.PC < romlength)
			{
				//Console.Write($"{i:x2} {i8080.mem8080[i8080.PC]:x2}"); i++; // memory demp to the terminal with each instruction i guess
				OpcodeHandler(i8080);
				i8080.PC++;
			}
			return i8080;
		}
	}
}