using SDL2;
using System.Diagnostics;

namespace cs8080
{
	class SpaceInvadersIO : State
	{

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
		static SDL.SDL_Event e;
		private static void Executor(SpaceInvadersIO i8080)
		{
			Stopwatch clock = new();
			while (i8080.PC < FM.ROMl)
			{
				while (SDL.SDL_PollEvent(out e) != 0)
				{
					if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
					{
						switch (e.key.keysym.scancode)
						{
							case SDL.SDL_Scancode.SDL_SCANCODE_C: i8080.port1 |= 0b1; break; // coin
							case SDL.SDL_Scancode.SDL_SCANCODE_1: i8080.port1 |= 0b100; break; // player 1 start
							case SDL.SDL_Scancode.SDL_SCANCODE_2: i8080.port1 |= 0b10; break; // player 2 start
							case SDL.SDL_Scancode.SDL_SCANCODE_SPACE: i8080.port1 |= 0b10000; break; // player 1 shoot
							case SDL.SDL_Scancode.SDL_SCANCODE_LEFT: i8080.port1 |= 0b100000; break; // player 1 left
							case SDL.SDL_Scancode.SDL_SCANCODE_RIGHT: i8080.port1 |= 0b1000000; break; // player 1 right
							case SDL.SDL_Scancode.SDL_SCANCODE_S: i8080.port2 |= 0b10000; break; // player 2 shoot
							case SDL.SDL_Scancode.SDL_SCANCODE_A: i8080.port2 |= 0b100000; break; // player 2 left
							case SDL.SDL_Scancode.SDL_SCANCODE_D: i8080.port2 |= 0b1000000; break; // player 2 right
						}
					}
					else if (e.type == SDL.SDL_EventType.SDL_KEYUP)
					{
						switch (e.key.keysym.scancode)
						{
							case SDL.SDL_Scancode.SDL_SCANCODE_C: i8080.port1 &= 0b11111110; break; // same as above
							case SDL.SDL_Scancode.SDL_SCANCODE_2: i8080.port1 &= 0b11111101; break;
							case SDL.SDL_Scancode.SDL_SCANCODE_1: i8080.port1 &= 0b11111011; break;
							case SDL.SDL_Scancode.SDL_SCANCODE_SPACE: i8080.port1 &= 0b11101111; break;
							case SDL.SDL_Scancode.SDL_SCANCODE_LEFT: i8080.port1 &= 0b11011111; break;
							case SDL.SDL_Scancode.SDL_SCANCODE_RIGHT: i8080.port1 &= 0b10111111; break;
							case SDL.SDL_Scancode.SDL_SCANCODE_S: i8080.port2 &= 0b11101111; break;
							case SDL.SDL_Scancode.SDL_SCANCODE_A: i8080.port2 &= 0b11011111; break;
							case SDL.SDL_Scancode.SDL_SCANCODE_D: i8080.port2 &= 0b10111111; break;
						}
					}
				}
				Emulate.OpcodeHandler(i8080);
				//Thread.Sleep(50);
			}
		}

		public static void SIrun(string rompath)
		{
			SpaceInvadersIO i8080 = new();
			Console.WriteLine("Loading Space Invaders...");
			i8080.mem8080 = FM.LoadROM(File.ReadAllBytes(rompath), i8080.mem8080, 0x0);
			Console.WriteLine($"Finished loading, running...");
			Executor(i8080);
		}
	}
}