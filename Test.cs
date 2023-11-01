namespace cs8080
{
	class cs8080test
	{
		public static void test(string testdir)
		{
			State i8080 = new();
			try
			{
				byte[] rom = File.ReadAllBytes($"{testdir}/cpudiag.bin"); // TST8080.COM
				Console.WriteLine("Loading ROM...");
				i8080.mem8080 = FM.LoadROM(rom, i8080.mem8080, 0x100);
				FM.DumpAll(i8080, "dump");
				i8080.PC = 0x100;

				//Fix the first instruction to be JMP 0x100    
				i8080.mem8080[0] = 0xc3;
				i8080.mem8080[1] = 0;
				i8080.mem8080[2] = 0x01;

				//Fix the stack pointer from 0x6ad to 0x7ad    
				// this 0x06 byte 112 in the code, which is    
				// byte 112 + 0x100 = 368 in memory    
				i8080.mem8080[368] = 0x7;

				Console.WriteLine("Done, running...");
				Emulate.Executor(i8080, (ushort)(rom.Length+i8080.PC));
			}
			catch (Exception E)
			{
				Console.WriteLine($"Crash at {Disassembler.OPlookup(i8080.mem8080[i8080.PC], i8080.mem8080[i8080.PC+1], i8080.mem8080[i8080.PC+2])}");
				Console.WriteLine(E);
				FM.DumpAll(i8080, "dump");
			}
		}
	}
}