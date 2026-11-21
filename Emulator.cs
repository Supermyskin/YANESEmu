using System;
using System.Threading;

public partial class Emulator
{
    public ushort ProgramCounter;
    public byte A, X, Y, opcode;
    public byte stackPointer;
    public ushort addressBus;
    public bool futureCarry;
    public long TotalCycles = 7;
    private bool pageCrossed;
    private readonly NesApu apu = new();
    private int pendingDmaCycles;
    private bool cpuStepActive;
    private bool dmaTransferActive;
    private int cpuBusCycles;
    private byte dataBus;
    private bool irqPollPending;
    private bool irqPollDisable;
    private bool iFlagAtInstructionStart;
    private bool servicingInterrupt;
    private bool irqPollDeferred;
    public bool CPU_Halted;
    public bool IRQRequested { get; private set; }
    public bool flag_Carry, flag_Zero, flag_InterruptDisable, flag_Decimal, flag_Overflow, flag_Negative;
    public event Action<Emulator, ushort, byte>? InstructionExecuted;
    public event Action? FrameReady;
    public event Action<string>? Diagnostic;

    public void SetIRQLine(bool asserted)
    {
        IRQRequested = asserted;
        if (asserted && !flag_InterruptDisable && !irqPollDeferred) irqPollPending = true;
    }
    public AudioSampleBuffer AudioSamples => apu.Output;

    public byte Read(ushort addr)
    {
        TickCpuBus();
        byte value;
        if (addr is 0x4016 or 0x4017)
            value = ReadController(addr - 0x4016, dataBus);
        else if (addr == 0x4015)
            value = (byte)((apu.ReadStatus() & 0x0F) | (dataBus & 0x20));
        else if (addr < 0x2000)
        {
            value = RAM[addr % 0x800];
        }
        else if (addr >= 0x8000)
        {
            int romIndex = (addr - 0x8000) % ROM.Length;
            value = ROM[romIndex];
        }
        else if (addr is >= 0x2000 and < 0x4000)
        {
            value = ReadPpuRegister((ushort)(0x2000 | (addr & 7)), dataBus);
        }
        else
            value = dataBus;

        if (addr != 0x4015) dataBus = value;
        return value;
    }

    public ushort ReadWord(ushort addr)
    {
        ushort low = Read(addr);
        ushort high = Read((ushort)(addr + 1));
        return (ushort)((high << 8) | low);
    }

    public void Write(ushort addr, byte value)
    {
        TickCpuBus();
        dataBus = value;
        if (addr == 0x4014)
        {
            int start = value << 8;
            dmaTransferActive = true;
            for (int i = 0; i < 256; i++)
                WriteOamDmaByte(Read((ushort)(start + i)));
            dmaTransferActive = false;
            pendingDmaCycles += (int)(513 + (TotalCycles & 1));
        }
        else if (addr == 0x4016)
        {
            WriteControllerStrobe(value);
        }
        else if (addr is >= 0x4000 and <= 0x4017)
        {
            apu.WriteRegister(addr, value);
        }
        else if (addr < 0x2000)
        {
            RAM[addr % 0x800] = value;
        }
        else if (addr is >= 0x2000 and < 0x4000)
        {
            WritePpuRegister((ushort)(0x2000 | (addr & 7)), value);
        }
    }

    private void TickCpuBus()
    {
        if (!cpuStepActive || dmaTransferActive) return;
        cpuBusCycles++;
        ClockPpu(3);
    }

    public void Run(CancellationToken cancellationToken = default)
    {
        while (!CPU_Halted && !cancellationToken.IsCancellationRequested) Step();
    }

    public uint[] GetFrameBuffer() => CopyFrameBuffer();
}
