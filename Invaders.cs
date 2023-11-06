using SDL2;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;

namespace cs8080
{
	class SpaceInvadersIO : State
	{
		public SpaceInvadersIO(ushort memsize) : base(memsize) { }
		/*
			PORT 1
			bit function
			0   coin(0 when active)
			1   P2 start button
			2   P1 start button
			3   ?
			4   P1 shoot button
			5   P1 joystick left
			6   P1 joystick right
			7   ?
		*/
		public byte port1;

		/*
			PORT 2
			bit function
			0-1 'dipswitch' number of lives (00 being 3 and 11 being 6)
			2   tilt button
			3   'dipswitch' bonus lives
			4   P2 shoot button
			5   P2 joystick left
			6   P2 joystick right
			7   'dipswitch' coin info (0 means on)
		*/
		public byte port2;
		private byte shiftoffset,msbyte,lsbyte;
		override public byte PortIN(State i8080, byte port)
		{
			switch (port)
			{
				case 0: break; // not called by space invaders
				case 1: i8080.A = port1; break; // read port 1
				case 2: i8080.A = port2; break; // read port 2
				//case 3: i8080.A = (byte)((ushort)((msbyte << 8) | lsbyte) >> (8 - shiftoffset) & 0xff); break; // shifting mechanism output
			}
			return i8080.A;
		}
		override public void PortOUT(State i8080, byte port)
		{
			switch (port)
			{
				case 2: shiftoffset = (byte)(i8080.A & 0x07); break; // set shift offset
				case 3: break; // play a sound
				case 4: // shifts data around
					lsbyte = msbyte;
					msbyte = i8080.A;
					break;
				case 5: break; // play a sound from bank 2
				case 6: break; // some debug? port
			}
		}
	}

	class SpaceInvaders
	{
		private static SpaceInvadersIO invaders = new(16 * 1024); // 16kibs of ram as a byte array
		private static bool exit; // should we exit the program
		private static void CPUSTEP()
		{
			Stopwatch clock = new();
			clock.Start();
			while (!exit)
			{
				Emulate.Exec(invaders);
				// Throttle the CPU emulation if needed.
				if (invaders.cycles >= (State.CLOCKSPEED / 60))
				{
					clock.Stop();

					if (clock.Elapsed.TotalMilliseconds < 16.6)
					{
						var sleepForMs = 16.6 - clock.Elapsed.TotalMilliseconds;

						if (sleepForMs >= 1)
						{
							Thread.Sleep((int)sleepForMs);
						}
					}

					invaders.totalcycles = 0; // reset the total cycles
					clock.Restart();
				}
				Emulate.InterruptGen(invaders, 0xcd);
			}
		}
		private static SpaceInvadersIO SDLGUI()
		{
			Stopwatch clock = new();

			while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
			{
				if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
				{
					switch (e.key.keysym.scancode)
					{
						case SDL.SDL_Scancode.SDL_SCANCODE_C: invaders.port1 |= 0b1; break; // coin
						case SDL.SDL_Scancode.SDL_SCANCODE_1: invaders.port1 |= 0b100; break; // player 1 start
						case SDL.SDL_Scancode.SDL_SCANCODE_2: invaders.port1 |= 0b10; break; // player 2 start
						case SDL.SDL_Scancode.SDL_SCANCODE_SPACE: Console.WriteLine("space"); invaders.port1 |= 0b10000; break; // player 1 shoot
						case SDL.SDL_Scancode.SDL_SCANCODE_LEFT: invaders.port1 |= 0b100000; break; // player 1 left
						case SDL.SDL_Scancode.SDL_SCANCODE_RIGHT: invaders.port1 |= 0b1000000; break; // player 1 right
						case SDL.SDL_Scancode.SDL_SCANCODE_S: invaders.port2 |= 0b10000; break; // player 2 shoot
						case SDL.SDL_Scancode.SDL_SCANCODE_A: invaders.port2 |= 0b100000; break; // player 2 left
						case SDL.SDL_Scancode.SDL_SCANCODE_D: invaders.port2 |= 0b1000000; break; // player 2 right
					}
				}
				else if (e.type == SDL.SDL_EventType.SDL_KEYUP)
				{
					switch (e.key.keysym.scancode)
					{
						case SDL.SDL_Scancode.SDL_SCANCODE_C: invaders.port1 &= 0b11111110; break; // same as above
						case SDL.SDL_Scancode.SDL_SCANCODE_2: invaders.port1 &= 0b11111101; break;
						case SDL.SDL_Scancode.SDL_SCANCODE_1: invaders.port1 &= 0b11111011; break;
						case SDL.SDL_Scancode.SDL_SCANCODE_SPACE: invaders.port1 &= 0b11101111; break;
						case SDL.SDL_Scancode.SDL_SCANCODE_LEFT: invaders.port1 &= 0b11011111; break;
						case SDL.SDL_Scancode.SDL_SCANCODE_RIGHT: invaders.port1 &= 0b10111111; break;
						case SDL.SDL_Scancode.SDL_SCANCODE_S: invaders.port2 &= 0b11101111; break;
						case SDL.SDL_Scancode.SDL_SCANCODE_A: invaders.port2 &= 0b11011111; break;
						case SDL.SDL_Scancode.SDL_SCANCODE_D: invaders.port2 &= 0b10111111; break;
					}
				}
				else if (e.type == SDL.SDL_EventType.SDL_QUIT)
				{
					exit = true;
				}
			}
			return invaders;
		}

		private static SpaceInvadersIO Executor()
		{
			//Thread exec8080 = new(new ThreadStart(CPUSTEP));
			//exec8080.Start();
			SDLGUI();
			/*
			uint count = 0;
			while (count < count * State.CLOCKSPEED / 1000)
			{
				uint cyc = invaders.totalcycles;
				Emulate.OpcodeHandler(invaders, invaders.Mem[invaders.PC]);
				uint elapsed = invaders.totalcycles - cyc;
				count += elapsed;
				if (invaders.totalcycles >= State.CLOCKSPEED / 60 / 2)
				{
					invaders.totalcycles -= State.CLOCKSPEED / 60 / 2;

					Emulate.InterruptGen(invaders, invaders.interruptop);
					invaders.interruptop = (ushort)(invaders.interruptop == 0xcf ? 0xd7 : 0xcf);
				}
			}
			*/

#if DEBUG
			Console.WriteLine($"PC: {invaders.PC:X4}, AF: {invaders.A:X2}{Ops.B2F(invaders):X2}, BC: {invaders.B:X2}{invaders.C:X2}, DE: {invaders.D:X2}{invaders.E:X2}, HL: {invaders.H:X2}{invaders.L:X2}, SP: {invaders.SP:X4} - {Disassembler.OPlookup(invaders.Mem[invaders.PC], invaders.Mem[invaders.PC + 1], invaders.Mem[invaders.PC + 2])}");
			//FM.DumpAll(invaders, "dump");
#endif
			return invaders;
		}

		public static void SIrun(string rompath)
		{
			Console.WriteLine("Loading Space Invaders...");
			invaders.Mem = FM.LoadROM(File.ReadAllBytes(rompath), invaders.Mem, 0);
			FM.DumpAll(invaders, "dump");
			Console.WriteLine($"Finished loading, running...");

			// initialise an SDL window
			int sdlinit = SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO);
			nint window = SDL.SDL_CreateWindow("Space Invaders", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, 640, 480, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
			nint renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
			Executor();

		}
	}
}