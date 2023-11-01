namespace cs8080
{
    class Disassembler
    {
        static void Main(string[] args)
        {
            try
            {
                Disassembler8080.Disassembler("invaders");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Done, {ex}");
            }
        }
    }
}