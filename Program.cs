using System.Diagnostics;

namespace cs8080
{
    class Disassembler
    {
        public static void DisASMmain(string filepath)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            Disassembler8080.Disassembler(filepath);
            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;
            Console.WriteLine("; Took {0:00}:{1:00}:{2:0000} to disassemble", ts.Minutes, ts.Seconds, ts.Milliseconds);
        }
    }

    class Emulate
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
                    i8080 = Emulate8080.Executor(i8080);
                    FM.MemDumpP(i8080.mem8080, "dump.bin");
                    FM.StateDump(i8080, "statedump.bin");
                } 
                catch
                {
                    FM.MemDumpP(i8080.mem8080, "dump.bin");
                    FM.StateDump(i8080, "statedump.bin");
                }
            }
		}
	}
}