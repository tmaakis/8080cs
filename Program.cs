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
					byte[] rom = File.ReadAllBytes(args[0]);
					i8080.mem8080 = FM.LoadROM(rom, i8080.mem8080);
					//Console.WriteLine($"{rom.Length:x2}");
					i8080 = Emulate.Executor(i8080, (ushort)rom.Length);
					FM.DumpAll(i8080, "dump");
				}
				catch
				{
					FM.DumpAll(i8080, "dump");
				}
			}
		}
	}
}