using System;
using System.IO;

public class Emulator
{
    public ushort ProgramCounter;
    public byte A;
    public byte X;
    public byte Y;
    public byte opcode;

    public byte stackPointer;

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

    public ushort ReadWord(ushort addr)
    {
        ushort low = Read(addr);
        ushort high = Read((ushort)(addr + 1));
        return (ushort)((high << 8) | low);
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

    public void Push(byte value)
    {
        Write((ushort)(0x100 + stackPointer), value);
        stackPointer--;
    }

    public byte Pull()
    {
        stackPointer++;
        return Read((ushort)(0x100 + stackPointer));
    }

    private bool BranchCondition(byte op)
    {
        switch (op)
        {
            case 0x10: return !flag_Negative;       // BPL
            case 0x30: return flag_Negative;        // BMI
            case 0x50: return !flag_Overflow;       // BVC
            case 0x70: return flag_Overflow;        // BVS
            case 0x90: return !flag_Carry;          // BCC
            case 0xB0: return flag_Carry;           // BCS
            case 0xD0: return !flag_Zero;           // BNE
            case 0xF0: return flag_Zero;            // BEQ
            default: return false;
        }
    }

    public int Step()
    {
        if (CPU_Halted) return 0;

        opcode = Read(ProgramCounter++);
        ushort targetAddr;

        switch (opcode)
        {
            case 0x02: // HLT
                CPU_Halted = true;
                return 2;

            case 0xA0: // LDY Immediate
                Y = Read(ProgramCounter++);
                SetZeroNegative(Y);
                return 2;

            case 0xA2: // LDX Immediate
                X = Read(ProgramCounter++);
                SetZeroNegative(X);
                return 2;

            case 0xA9: // LDA Immediate
                A = Read(ProgramCounter++);
                SetZeroNegative(A);
                return 2;

            case 0x85: // STA Zero Page
                targetAddr = Read(ProgramCounter++);
                Write(targetAddr, A);
                return 3;

            case 0xA5: // LDA Zero Page
                targetAddr = Read(ProgramCounter++);
                A = Read(targetAddr);
                SetZeroNegative(A);
                return 3;

            case 0x86: // STX Zero Page
                targetAddr = Read(ProgramCounter++);
                Write(targetAddr, X);
                return 3;

            case 0x84: // STY Zero Page
                targetAddr = Read(ProgramCounter++);
                Write(targetAddr, Y);
                return 3;

            case 0x8D: // STA Absolute
                targetAddr = ReadWord(ProgramCounter);
                ProgramCounter += 2;
                Write(targetAddr, A);
                return 4;

            case 0xAD: // LDA Absolute
                targetAddr = ReadWord(ProgramCounter);
                ProgramCounter += 2;
                A = Read(targetAddr);
                SetZeroNegative(A);
                return 4;

            case 0x8E: // STX Absolute
                targetAddr = ReadWord(ProgramCounter);
                ProgramCounter += 2;
                Write(targetAddr, X);
                return 4;

            case 0x8C: // STY Absolute
                targetAddr = ReadWord(ProgramCounter);
                ProgramCounter += 2;
                Write(targetAddr, Y);
                return 4;

            case 0x48: //PHA
                Push(A);
                return 3;

            case 0x68: //PLA
                A = Pull();
                SetZeroNegative(A);
                return 4;

            // branch instuctions
            case 0x10: // BPL
            case 0x30: // BMI
            case 0x50: // BVC
            case 0x70: // BVS
            case 0x90: // BCC
            case 0xB0: // BCS
            case 0xD0: // BNE
            case 0xF0: // BEQ
                {
                    sbyte offset = (sbyte)Read(ProgramCounter++);
                    int branchCycles = 2;

                    if (BranchCondition(opcode))
                    {
                        ushort oldPC = ProgramCounter;
                        ProgramCounter = (ushort)(ProgramCounter + offset);
                        branchCycles++;

                        if ((oldPC & 0xFF00) != (ProgramCounter & 0xFF00))
                        {
                            branchCycles++;
                        }
                    }
                    return branchCycles;
                }

            default:
                Console.WriteLine($"Unknown opcode 0x{opcode:X2} at PC: 0x{(ProgramCounter - 1):X4}");
                CPU_Halted = true;
                return 2;
        }
    }

    public void Run()
    {
        while (!CPU_Halted)
        {
            Step();
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

        stackPointer = 0xFD;
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
            Array.Copy(fileBytes, prgOffset, ROM, 0, Math.Min(prgSize, ROM.Length));
        }

        ProgramCounter = 0;
        A = 0;
        X = 0;
        Y = 0;
        opcode = 0;

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
            string path = "/Users/damian/Programming/YANESEmu/3_Branches.nes";

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