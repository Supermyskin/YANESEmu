using System;
using System.IO;

public class Emulator
{
    public ushort ProgramCounter;
    public byte A;
    public byte X;
    public byte Y;
    public byte opcode;

    public bool CPU_Halted = false;

    public bool flag_Carry;
    public bool flag_Zero;
    public bool flag_InterruptDisable;
    public bool flag_Decimal;
    public bool flag_Overflow;
    public bool flag_Negative;

    public byte[] RAM = new byte[0x800];
    public byte[] ROM = new byte[0x8000];

    public byte Read(ushort addr)
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

    public void Write(ushort addr, byte value)
    {
        if (addr < 0x2000)
        {
            RAM[addr % 0x800] = value;
        }
    }

    public void Emulate_CPU()
    {
        opcode = Read(ProgramCounter);
        ProgramCounter++;

        switch (opcode)
        {
            case 0x02:
                CPU_Halted = true;
                break;

            case 0xA0:
                {
                    Y = Read(ProgramCounter);
                    flag_Zero = (Y == 0);
                    flag_Negative = (Y & 0x80) != 0;
                    ProgramCounter++;
                }
                break;

            case 0xA2:
                {
                    X = Read(ProgramCounter);
                    flag_Zero = (X == 0);
                    flag_Negative = (X & 0x80) != 0;
                    ProgramCounter++;
                }
                break;

            case 0xA9:
                {
                    A = Read(ProgramCounter);
                    flag_Zero = (A == 0);
                    flag_Negative = (A & 0x80) != 0;
                    ProgramCounter++;
                }
                break;

            case 0x85:
                {
                    byte temp = Read(ProgramCounter);
                    ProgramCounter++;
                    Write(temp, A);
                }
                break;

            case 0x8D:
                {
                    byte tempLow = Read(ProgramCounter);
                    ProgramCounter++;
                    byte tempHigh = Read(ProgramCounter);
                    ProgramCounter++;
                    ushort targetAddr = (ushort)((tempHigh << 8) | tempLow);
                    Write(targetAddr, A);
                }
                break;

            case 0xA5:
                {
                    byte temp = Read(ProgramCounter);
                    ProgramCounter++;
                    A = Read(temp);
                    flag_Zero = (A == 0);
                    flag_Negative = (A & 0x80) != 0;
                }
                break;

            case 0xAD:
                {
                    byte tempLow = Read(ProgramCounter);
                    ProgramCounter++;
                    byte tempHigh = Read(ProgramCounter);
                    ProgramCounter++;
                    ushort targetAddr = (ushort)((tempHigh << 8) | tempLow);
                    A = Read(targetAddr);
                    flag_Zero = (A == 0);
                    flag_Negative = (A & 0x80) != 0;
                }
                break;

            case 0x86:
                {
                    byte temp = Read(ProgramCounter);
                    ProgramCounter++;
                    Write(temp, X);
                }
                break;

            case 0x8E:
                {
                    byte tempLow = Read(ProgramCounter);
                    ProgramCounter++;
                    byte tempHigh = Read(ProgramCounter);
                    ProgramCounter++;
                    ushort targetAddr = (ushort)((tempHigh << 8) | tempLow);
                    Write(targetAddr, X);
                }
                break;

            case 0x84:
                {
                    byte temp = Read(ProgramCounter);
                    ProgramCounter++;
                    Write(temp, Y);
                }
                break;

            case 0x8C:
                {
                    byte tempLow = Read(ProgramCounter);
                    ProgramCounter++;
                    byte tempHigh = Read(ProgramCounter);
                    ProgramCounter++;
                    ushort targetAddr = (ushort)((tempHigh << 8) | tempLow);
                    Write(targetAddr, Y);
                }
                break;

            default:
                CPU_Halted = true;
                break;
        }
    }

    public void Run()
    {
        while (!CPU_Halted)
        {
            Emulate_CPU();
        }
    }

    public void Reset(string filepath)
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

        flag_InterruptDisable = true;
        
        byte pcLow = Read(0xFFFC);
        byte pcHigh = Read(0xFFFD);
        ProgramCounter = (ushort)((pcHigh * 0x100) + pcLow);

        if (ProgramCounter == 0x0000 || ProgramCounter == 0xFFFF)
        {
            ProgramCounter = 0x8000;
        }
    }

    class Program 
    {
        static void Main(string[] args)
        {
            Emulator emulator = new Emulator();
            string path = "/Users/damian/Programming/YANESEmu/2_ReadWrite.nes";
            
            emulator.Reset(path);
            emulator.Run();
            
            Console.WriteLine($"Emulator halted. Final PC: 0x{emulator.ProgramCounter:X4}");
            Console.WriteLine($"A : 0x{emulator.A:X2} ({emulator.A})");
            Console.WriteLine($"X : 0x{emulator.X:X2} ({emulator.X})");
            Console.WriteLine($"Y : 0x{emulator.Y:X2} ({emulator.Y})");
            Console.WriteLine($"RAM[0x00] = 0x{emulator.RAM[0x00]:X2}");
            Console.WriteLine($"RAM[0x01] = 0x{emulator.RAM[0x01]:X2}");
            Console.WriteLine($"RAM[0x02] = 0x{emulator.RAM[0x02]:X2}");
        }
    }
}