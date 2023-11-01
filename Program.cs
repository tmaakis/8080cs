namespace cs8080
{
    class Disassembler
    {
        static void Main(string[] args)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                Disassembler8080.Disassembler("invaders");
            }
            catch
            {
                Console.WriteLine("");
                stopwatch.Stop();
                TimeSpan ts = stopwatch.Elapsed;
                Console.WriteLine("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            }
        }
    }
}