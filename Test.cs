﻿using System.Text;

namespace cs8080
{
	class TestIO : State
	{
		public TestIO(ushort memsize) : base(memsize) {} // method to set memory size of the base class from this class

		override public byte PortIN(State i8080, byte port)
		{
			i8080.A = 0x00;
			return i8080.A;
		}
		override public void PortOUT(State i8080, byte port)
		{
			if (port == 0)
			{
				i8080.halt=true;
			}
			else if (port == 1)
			{
				if (i8080.C == 9)
				{
					ushort addr = (ushort)((i8080.D << 8) | i8080.E);
					do
					{
						Console.Write((char)i8080.Mem[addr++]);
					} while ((char)i8080.Mem[addr] != '$');
				}
				else if (i8080.C == 2)
				{
					Console.Write((char)i8080.E);
				}
			}
		}
	}
	class Test8080
	{
		public static void Test(string testdir)
		{
            //string[] roms = { "TST8080.COM", "CPUTEST.COM", "8080PRE.COM", "8080EXM.COM" };
            string[] roms = { "TST8080.COM" };
            foreach (string romp in roms)
			{
                Console.WriteLine($"\nLoading ROM ({romp})");
                byte[] rom = File.ReadAllBytes($"{testdir}/{romp}");
                TestIO i8080 = new(65535);
				try
				{
					i8080.Mem = FM.LoadROM(rom, i8080.Mem, 0x100);
					FM.DumpAll(i8080, "dump");
					i8080.PC = 0x100;

					// inject "out 0,a" at 0x0000 (signal to stop the test)
					i8080.Mem[0x0000] = 0xD3;
					i8080.Mem[0x0001] = 0x00;

					// inject "out 1,a" at 0x0005 (signal to output some characters)
					i8080.Mem[0x0005] = 0xD3;
					i8080.Mem[0x0006] = 0x01;
					i8080.Mem[0x0007] = 0xC9;

					// skip DAA bc aux carry isn't working properly yet
					//i8080.Mem[0x59c] = 0xc3; 
					//i8080.Mem[0x59d] = 0xc2;
					//i8080.Mem[0x59e] = 0x05;

					Console.WriteLine($"Done loading {FM.ROMl} bytes, running...");
					while (!i8080.halt)
					{
					Emulate.Exec(i8080);
					}
				}
				catch (Exception E)
				{
					//Console.WriteLine($"Crash at {Disassembler.OPlookup(i8080.Mem[i8080.PC], i8080.Mem[i8080.PC + 1], i8080.Mem[i8080.PC + 2])}");
					Console.WriteLine(E);
				}
				finally
				{
                    //FM.DumpAll(i8080, "dump");
                }
			}
		}
	}
}
