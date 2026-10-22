using System;
using System.IO;

public class Emulator
{
    ushort ProgramCounter;
    byte A;
    byte X;
    byte Y;
    byte opcode;

    int cycle = 0; //CPU Cycles for when dealing with PPU X opcodes
    bool CPU_Halted = false;


    byte[] RAM = new byte[0x800];
    byte[] ROM = new byte[0x8000];

    byte Read(ushort addr)
    {
        if (addr < 0x800)
        {
            return RAM[addr];
        }
        if (addr >= 0x8000)
        {
            int romIndex = (addr - 0x8000) % ROM.Length;
            return ROM[romIndex];
        }
        return 0;
    }

    void Emulate_CPU()
    {
        if (cycle == 0)
        {
            byte opcode = Read(ProgramCounter);
            ProgramCounter++;
            cycle++;
        }
        else {
            switch (opcode) // The humble beginnings of a thousand-line switch statement.
            {
                case 0x02: //HLT
                    CPU_Halted = true;
                    break;
                case 0xA0: //LDY Immediate
                    Y = Read(ProgramCounter);
                    ProgramCounter++;
                    cycle = 0;
                    break;
                case 0xA2: //LDX Immediate
                    X = Read(ProgramCounter);
                    ProgramCounter++;
                    cycle = 0;
                    break;
                case 0xA9: //LDA Immediate
                    A = Read(ProgramCounter);
                    ProgramCounter++;
                    cycle = 0; // Boom ready to start the next opcode
                    break;
                default: //Unknown opcode???
                    break;
            }
        }
    }

    void Run()
    {
        while (!CPU_Halted)
        {
            Emulate_CPU();
        }
    }
    void Reset(string filepath)
    {
        if (!File.Exists(filepath))
        {
            Console.WriteLine($"Error: File not found at {filepath}");
            return;
        }

        byte[] fileBytes = File.ReadAllBytes(filepath);

        int prgPages = fileBytes[4];
        int prgSize = prgPages * 16384;

        if (prgSize == 16384)
        {
            Array.Copy(fileBytes, 0x10, ROM, 0, 16384);
            Array.Copy(fileBytes, 0x10, ROM, 16384, 16384);
        }
        else
        {
            Array.Copy(fileBytes, 0x10, ROM, 0, Math.Min(prgSize, ROM.Length));
        }

        byte programCounterLow = Read(0xFFFC);
        byte programCounterHigh = Read(0xFFFD);
        ProgramCounter = (ushort)((programCounterHigh * 0x100) + programCounterLow);
    }
    class Program
    {
        static void Main(string[] args)
        {
            Emulator emulator = new Emulator();
            string path = "/Users/damian/Programming/YANESEmu/1_Example.nes";
            emulator.Reset(path);
            emulator.Run();
            Console.WriteLine($"Emulator reset successfully. PC: 0x{emulator.ProgramCounter:X4}");
        }
    }
}
