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
			        i8080.mem8080 = FM.ToRAM(args[0], i8080.mem8080);
                    i8080 = Emulate.Executor(i8080);
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