using System.Globalization;
using System.Text;

namespace cs8080
{
	class FM
	{
		public static ushort ROMl=0;

		public static byte[] LoadROM(byte[] rom, byte[] mem, int offset)
		{
			for (int i = rom.Length - 1; rom[i] == 0x00; i--)
			{
				ROMl++;
			}
			ROMl = (ushort)(rom.Length + offset - ROMl);
			for (int i = offset; i < ROMl; i++)
			{
				mem[i] = rom[i - offset];
			}
			return mem;
		}

		private static void MemDumpP(byte[] mem, string Dumpfilename)
		{
			string OldDumpfilename = $"{Dumpfilename}.old";
			if (File.Exists(Dumpfilename))
			{
				if (File.Exists(OldDumpfilename))
				{
					File.Delete(OldDumpfilename);
				}
				File.Move(Dumpfilename, OldDumpfilename);
			}
			File.WriteAllBytes($"{Dumpfilename}", mem);
		}

		public static void StateDump(State i8080, string Dumpfilename) // debug.sh should be able to just read this file it creates
		{
			StringBuilder dump = new();
			dump.Append($"8080cs @ {DateTime.Now.ToString(new CultureInfo("en-GB"))} \n \n");
			dump.Append($"Instruction: \n{Disassembler.OPlookup(i8080.Mem[i8080.PC], i8080.Mem[i8080.PC+1], i8080.Mem[i8080.PC+2])} \n \n");
			dump.Append($"Condition Codes: \nSign = {Convert.ToByte(i8080.CC.S)} \nZero = {Convert.ToByte(i8080.CC.Z)} \nParity = {Convert.ToByte(i8080.CC.P)} \n");
			dump.Append($"Carry = {Convert.ToByte(i8080.CC.CY)} \nAux Carry = {Convert.ToByte(i8080.CC.AC)} \n");
			dump.Append($"\nRegisters: \nB = {i8080.B:x2} \nC = {i8080.C:x2} \nD = {i8080.D:x2} \nE = {i8080.E:x2} \nH = {i8080.H:x2} \nL = {i8080.L:x2} \nA = {i8080.A:x2} \n");
			dump.Append($"\nStack Pointer: \n{i8080.SP:x4} \n");
			dump.Append($"Program Counter: \n{i8080.PC:x4} \n");
			File.WriteAllText(Dumpfilename, dump.ToString());
		}

		public static void DumpAll(State i8080, string Dumpfilename)
		{
			MemDumpP(i8080.Mem, $"{Dumpfilename}.bin");
			StateDump(i8080, $"state{Dumpfilename}.txt");
		}
	}
}