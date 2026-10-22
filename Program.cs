using System;
using System.IO;

public class Emulator {
    public ushort ProgramCounter;
    public byte A;
    public byte X;
    public byte Y;

    public byte[] RAM = new byte[0x800];
    public byte[] ROM = new byte[0x8000]; // 32KB PRG-ROM space

    public byte Read(ushort addr) {
        if (addr < 0x800) {
            return RAM[addr];
        }
        if (addr >= 0x8000) {
            // Automatically handle mirroring if the ROM is 16KB instead of 32KB
            int romIndex = (addr - 0x8000) % ROM.Length;
            return ROM[romIndex];
        }
        return 0;
    }

    public void Reset(string filepath) {
        if (!File.Exists(filepath)) {
            Console.WriteLine($"Error: File not found at {filepath}");
            return;
        }

        byte[] fileBytes = File.ReadAllBytes(filepath);
        
        // iNES header byte 4 specifies the number of 16KB PRG-ROM banks
        int prgPages = fileBytes[4]; 
        int prgSize = prgPages * 16384;

        if (prgSize == 16384) {
            // 16KB ROM: Load it and mirror it into both halves of the 32KB space
            Array.Copy(fileBytes, 0x10, ROM, 0, 16384);
            Array.Copy(fileBytes, 0x10, ROM, 16384, 16384);
        } else {
            // 32KB ROM
            Array.Copy(fileBytes, 0x10, ROM, 0, Math.Min(prgSize, ROM.Length));
        }

        // Fetch Reset Vector from 0xFFFC and 0xFFFD
        byte programCounterLow = Read(0xFFFC);
        byte programCounterHigh = Read(0xFFFD);       
        ProgramCounter = (ushort)((programCounterHigh * 0x100) + programCounterLow);
    }
}

class Program {
    static void Main(string[] args) {
        Emulator emulator = new Emulator();
        string path = "/Users/damian/Programming/YANESEmu/1_Example.nes";        
        emulator.Reset(path);
        Console.WriteLine($"Emulator reset successfully. PC: 0x{emulator.ProgramCounter:X4}");
    }
}