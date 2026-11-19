using System;

public partial class Emulator
{
    public ushort ProgramCounter;
    public byte A, X, Y, opcode;
    public byte stackPointer;
    public ushort addressBus;
    public bool futureCarry;
    public long TotalCycles = 7;
    private bool pageCrossed;
    public bool CPU_Halted;
    public bool flag_Carry, flag_Zero, flag_InterruptDisable, flag_Decimal, flag_Overflow, flag_Negative;
    public event Action<Emulator, ushort, byte>? InstructionExecuted;
    public event Action? FrameReady;
    public event Action<string>? Diagnostic;

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
        if (addr is >= 0x2000 and < 0x4000)
        {
            return ReadPpuRegister((ushort)(0x2000 | (addr & 7)));
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
        else if (addr is >= 0x2000 and < 0x4000)
        {
            WritePpuRegister((ushort)(0x2000 | (addr & 7)), value);
        }
    }

    public void Run()
    {
        while (!CPU_Halted) Step();
    }

    public uint[] GetFrameBuffer() => CopyFrameBuffer();
}
