using System.Globalization;
using System.Text;

namespace cs8080
{
	class FM
	{
		public static byte[] LoadROM(byte[] rom, byte[] mem)
		{
			for (int i = 0; i < rom.Length; i++)
			{
				mem[i] = rom[i];
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
			File.WriteAllBytes("dump.bin", mem);
		}

		private static void StateDump(State i8080, string Dumpfilename) // debug.sh should be able to just read this file it creates
		{
			StringBuilder dump = new();
			dump.Append($"8080cs @ {DateTime.Now.ToString(new CultureInfo("en-GB"))} \n \n");
			dump.Append($"Condition Codes: \nSign = {Convert.ToByte(i8080.S)} \nZero = {Convert.ToByte(i8080.Z)} \nParity = {Convert.ToByte(i8080.P)} \n");
			dump.Append($"Carry = {Convert.ToByte(i8080.CY)} \nAux Carry = {Convert.ToByte(i8080.AC)} \n");
			dump.Append($"\nRegisters: \nB = {i8080.B:x2} \nC = {i8080.C:x2} \nD = {i8080.B:x2} \nE = {i8080.E:x2} \nH = {i8080.H:x2} \nL = {i8080.L:x2} \nM = {i8080.M:x2} \nA = {i8080.A:x2} \n");
			dump.Append($"\nStack Pointer: \n{i8080.SP:x4} \n");
			dump.Append($"Program Counter: \n{i8080.PC:x4} \n");
			File.WriteAllText(Dumpfilename, dump.ToString());
		}

		public static void DumpAll(State i8080, string Dumpfilename)
		{
			MemDumpP(i8080.mem8080, $"{Dumpfilename}.bin");
			StateDump(i8080, $"state{Dumpfilename}.txt");
		}
	}
}