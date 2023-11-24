namespace cs8080
{
	class Main8080
	{
        static void Main(string[] args)
		{
			string[] prargs = new string[2];
			try
			{
				prargs[0] = args[0];
				prargs[1] = args[1];
			}
			catch
			{
				prargs[0] ??= "test";
				prargs[1] ??= @"..\..\..\";
			}
            switch (prargs[0])
			{
			case "disassemble": Disassembler.DisASMmain(prargs[1]); break;
			case "test": Test8080.Test(prargs[1]); break;
			case "invaders": SpaceInvaders.SIrun("invaders.bin"); break;
			default:
				State i8080 = new(65535); // init 8080 state with 64k of ram, which is the max
				try
				{
					Console.WriteLine("Loading ROM...");
					i8080.Mem = FM.LoadROM(File.ReadAllBytes(prargs[0]), i8080.Mem, 0x0);
					Console.WriteLine($"Done loading {FM.ROMl} bytes, running...");
					Emulate.Executor(i8080);
#if DEBUG
					FM.DumpAll(i8080, "dump");
#endif
				}
				catch (Exception E)
				{
					Console.WriteLine($"Crash at {Disassembler.OPlookup(i8080.Mem[i8080.PC], i8080.Mem[i8080.PC+1], i8080.Mem[i8080.PC+2])}");
					Console.WriteLine(E);
					FM.DumpAll(i8080, "dump");
				}
				break;
			}
		}
	}
}