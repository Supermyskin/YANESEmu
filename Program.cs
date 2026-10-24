using System;
using System.IO;

public class Emulator
{
    public ushort ProgramCounter;
    public byte A;
    public byte X;
    public byte Y;
    public byte opcode;
    public int cycles;

    public bool CPU_Halted = false;

    public bool flag_Carry;
    public bool flag_Zero;
    public bool flag_InterruptDisable;
    public bool flag_Decimal;
    public bool flag_Overflow;
    public bool flag_Negative;

    public byte[] RAM = new byte[0x800];
    public byte[] ROM = new byte[0x8000];

    private byte temp;
    private byte tempLow;
    private byte tempHigh;
    private ushort targetAddr;
    private ushort tempPC;

    public byte Read(ushort addr)
    {
        if (addr < 0x2000)
        {
            return RAM[addr % 0x800];
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

    private void SetZeroNegative(byte value)
    {
        flag_Zero = (value == 0);
        flag_Negative = (value & 0x80) != 0;
    }

    private bool BranchCondition()
    {
        switch (opcode)
        {
            case 0x10: // BPL
                return !flag_Negative;

            case 0x30: // BMI
                return flag_Negative;

            case 0x50: // BVC
                return !flag_Overflow;

            case 0x70: // BVS
                return flag_Overflow;

            case 0x90: // BCC
                return !flag_Carry;

            case 0xB0: // BCS
                return flag_Carry;

            case 0xD0: // BNE
                return !flag_Zero;

            case 0xF0: // BEQ
                return flag_Zero;

            default:
                return false;
        }
    }

    public void Emulate_CPU()
    {
        int signedVal;

        if (CPU_Halted)
        {
            return;
        }

        if (cycles == 0)
        {
            opcode = Read(ProgramCounter);
            ProgramCounter++;
            cycles = 1;

            if (opcode == 0x02)
            {
                CPU_Halted = true;
                cycles = 0;
            }

            return;
        }

        switch (opcode)
        {
            case 0xA0: // LDY Immediate
                if (cycles == 1)
                {
                    Y = Read(ProgramCounter);
                    ProgramCounter++;
                    SetZeroNegative(Y);
                    cycles = 0;
                }
                break;

            case 0xA2: // LDX Immediate
                if (cycles == 1)
                {
                    X = Read(ProgramCounter);
                    ProgramCounter++;
                    SetZeroNegative(X);
                    cycles = 0;
                }
                break;

            case 0xA9: // LDA Immediate
                if (cycles == 1)
                {
                    A = Read(ProgramCounter);
                    ProgramCounter++;
                    SetZeroNegative(A);
                    cycles = 0;
                }
                break;

            case 0x85: // STA Zero Page
                if (cycles == 1)
                {
                    temp = Read(ProgramCounter);
                    ProgramCounter++;
                    targetAddr = temp;
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    Write(targetAddr, A);
                    cycles = 0;
                }
                break;

            case 0x8D: // STA Absolute
                if (cycles == 1)
                {
                    tempLow = Read(ProgramCounter);
                    ProgramCounter++;
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    tempHigh = Read(ProgramCounter);
                    ProgramCounter++;
                    targetAddr = (ushort)((tempHigh << 8) | tempLow);
                    cycles = 3;
                }
                else if (cycles == 3)
                {
                    Write(targetAddr, A);
                    cycles = 0;
                }
                break;

            case 0xA5: // LDA Zero Page
                if (cycles == 1)
                {
                    temp = Read(ProgramCounter);
                    ProgramCounter++;
                    targetAddr = temp;
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    A = Read(targetAddr);
                    SetZeroNegative(A);
                    cycles = 0;
                }
                break;

            case 0xAD: // LDA Absolute
                if (cycles == 1)
                {
                    tempLow = Read(ProgramCounter);
                    ProgramCounter++;
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    tempHigh = Read(ProgramCounter);
                    ProgramCounter++;
                    targetAddr = (ushort)((tempHigh << 8) | tempLow);
                    cycles = 3;
                }
                else if (cycles == 3)
                {
                    A = Read(targetAddr);
                    SetZeroNegative(A);
                    cycles = 0;
                }
                break;

            case 0x86: // STX Zero Page
                if (cycles == 1)
                {
                    temp = Read(ProgramCounter);
                    ProgramCounter++;
                    targetAddr = temp;
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    Write(targetAddr, X);
                    cycles = 0;
                }
                break;

            case 0x8E: // STX Absolute
                if (cycles == 1)
                {
                    tempLow = Read(ProgramCounter);
                    ProgramCounter++;
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    tempHigh = Read(ProgramCounter);
                    ProgramCounter++;
                    targetAddr = (ushort)((tempHigh << 8) | tempLow);
                    cycles = 3;
                }
                else if (cycles == 3)
                {
                    Write(targetAddr, X);
                    cycles = 0;
                }
                break;

            case 0x84: // STY Zero Page
                if (cycles == 1)
                {
                    temp = Read(ProgramCounter);
                    ProgramCounter++;
                    targetAddr = temp;
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    Write(targetAddr, Y);
                    cycles = 0;
                }
                break;

            case 0x8C: // STY Absolute
                if (cycles == 1)
                {
                    tempLow = Read(ProgramCounter);
                    ProgramCounter++;
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    tempHigh = Read(ProgramCounter);
                    ProgramCounter++;
                    targetAddr = (ushort)((tempHigh << 8) | tempLow);
                    cycles = 3;
                }
                else if (cycles == 3)
                {
                    Write(targetAddr, Y);
                    cycles = 0;
                }
                break;

            case 0x10: // BPL
            case 0x30: // BMI
            case 0x50: // BVC
            case 0x70: // BVS
            case 0x90: // BCC
            case 0xB0: // BCS
            case 0xD0: // BNE
            case 0xF0: // BEQ
                if (cycles == 1)
                {
                    temp = Read(ProgramCounter);
                    ProgramCounter++;

                    if (!BranchCondition())
                    {
                        cycles = 0;
                        break;
                    }

                    tempPC = ProgramCounter;

                    signedVal = temp;
                    if (signedVal > 127)
                    {
                        signedVal -= 256;
                    }

                    targetAddr = (ushort)(ProgramCounter + signedVal);
                    cycles = 2;
                }
                else if (cycles == 2)
                {
                    Read(tempPC);

                    if ((tempPC & 0xFF00) != (targetAddr & 0xFF00))
                    {
                        ProgramCounter = (ushort)(
                            (tempPC & 0xFF00) |
                            (targetAddr & 0x00FF));

                        cycles = 3;
                    }
                    else
                    {
                        ProgramCounter = targetAddr;
                        cycles = 0;
                    }
                }
                else if (cycles == 3)
                {
                    Read(ProgramCounter);
                    ProgramCounter = targetAddr;
                    cycles = 0;
                }
                break;

            default:
                CPU_Halted = true;
                cycles = 0;
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

        if (fileBytes.Length < 16 ||
            fileBytes[0] != 0x4E ||
            fileBytes[1] != 0x45 ||
            fileBytes[2] != 0x53 ||
            fileBytes[3] != 0x1A)
        {
            Console.WriteLine("Error: Invalid iNES ROM file");
            return;
        }

        int prgPages = fileBytes[4];
        int prgSize = prgPages * 16384;
        int prgOffset = 0x10;

        if ((fileBytes[6] & 0x04) != 0)
        {
            prgOffset += 512;
        }

        if (fileBytes.Length < prgOffset + prgSize)
        {
            Console.WriteLine("Error: ROM file is incomplete");
            return;
        }

        Array.Clear(ROM, 0, ROM.Length);

        if (prgSize == 16384)
        {
            Array.Copy(fileBytes, prgOffset, ROM, 0, 16384);
            Array.Copy(fileBytes, prgOffset, ROM, 16384, 16384);
        }
        else
        {
            Array.Copy(fileBytes, prgOffset, ROM, 0,
                Math.Min(prgSize, ROM.Length));
        }

        ProgramCounter = 0;
        A = 0;
        X = 0;
        Y = 0;
        opcode = 0;
        cycles = 0;

        CPU_Halted = false;

        flag_Carry = false;
        flag_Zero = false;
        flag_InterruptDisable = true;
        flag_Decimal = false;
        flag_Overflow = false;
        flag_Negative = false;

        Array.Clear(RAM, 0, RAM.Length);

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
