namespace cs8080
{
	class cs8080main
	{
		static void Main(string[] args)
		{
			if (args[0] == "disassemble")
			{
				Disassembler.DisASMmain(args[1]);
			}
			else
			{
				State i8080 = new();
				try
				{
					if (args[0] == "test")
					{
						cs8080test.test(args[1]);
					}
					else
					{
						Console.WriteLine("Loading ROM...");
						i8080.mem8080 = FM.LoadROM(File.ReadAllBytes(args[0]), i8080.mem8080, 0x0);
						Console.WriteLine($"Done loading {FM.ROMl} bytes, running...");
						Emulate.Executor(i8080, 0);
						FM.DumpAll(i8080, "dump");
					}
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
}