# 8080cs
An intel 8080 emulator, written in C#.

This program is probably not written in a way that is meant to be written in, judging by looking at other projects of other C# emulators, but it does work. 

Currently, Aux Carry does not work, so the test won't pass. You can skip the test by altering the memory, in Test.cs

## Running 
### 1. Install prerequisites & clone repo 
  - (for spate invaders) Install SDL2
  - Clone a enter this repo (`git clone https://github.com/tmaakis/8080.git && cd 8080cs')
### 2. Run the progran 
There are a few ways to run the emulator. To run the test program (looks for TST0080.COX), simply type 
```
dotnet run test path/to/testrom -c Release
```
To run space invaders (incomplete) 
```
dotnet run invaders.bin -c Release # where invaders.bin is the 4 Space Invader rom parts put together
```
Remove the '-c Release" if you want debug output to the terminal and memory dumps.

You can also run the disassembler on its own by running: 
```
dotnet run disassemble path/to/romtodisassemble 
```
## Resources used 
* [Official 8080 Programmer's Manual](https://altairclone.com/downloads/manuals/8080%20Programmers%20Manual.pdf)
* [Emulator101 guide & opcode reference](http://www.emulator101.com/reference/8080-by-opcode.html)
* [computerarcheology.com's space invaders info page](https://computerarcheology.com/Arcade/SpaceInvaders/)
* [Intel 8080/Space Invaders emu in C by superzazu](https://github.com/superzazu/8080)
* [Test binary](https://altairclone.com/downloads/cpu_tests/TST8080.COM)
* [Justin-Credible's Intel 8080/Space invaders emu in C#](https://www.justin-credible.net/2020/03/31/space-invaders-emulator/)
