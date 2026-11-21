using System;
using System.IO;
using System.Threading;

Run("CPU executes immediate load and transfer", CpuExecutesLoadAndTransfer);
Run("CPU run loop honors cancellation after the current instruction", CpuRunStopsOnCancellation);
Run("CPU recognizes every official NMOS 6502 opcode", CpuRecognizesOfficialOpcodes);
Run("ADC indirect-indexed timing adds a page-cross cycle", AdcIndirectYPageCrossTiming);
Run("indexed reads account for page crossings", IndexedReadPageCrossTiming);
Run("conditional branches account for taken and page-cross cycles", BranchCycleTiming);
Run("CPU RAM mirrors every 0x800 bytes", CpuRamMirrors);
Run("PPU data reads are buffered", PpuDataReadsAreBuffered);
Run("PPU universal palette entries mirror", PpuPaletteEntriesMirror);
Run("PPU raises NMI at vblank", PpuRaisesNmiAtVblank);
Run("NMI enters through its vector and pushes hardware status", PpuNmiIsServiced);
Run("IRQ obeys the interrupt mask and preserves the asserted line", IrqIsMaskedAndServiced);
Run("BRK pushes the software-interrupt status", BrkPushesBreakStatus);
Run("NROM cartridge loads reset vector and CHR RAM", NromLoads);
Run("emulator publishes frames without console output", EmulatorPublishesFrames);
Run("unsupported opcodes publish diagnostics", UnsupportedOpcodesPublishDiagnostics);
Console.WriteLine("All emulator checks passed.");

static void Run(string name, Action test)
{
    test();
    Console.WriteLine($"PASS {name}");
}

static void CpuExecutesLoadAndTransfer()
{
    Emulator emulator = NewEmulator(0xA9, 0x80, 0xAA, 0x02);
    emulator.Step();
    Equal((byte)0x80, emulator.A, "LDA immediate value");
    True(emulator.flag_Negative, "LDA negative flag");
    emulator.Step();
    Equal((byte)0x80, emulator.X, "TAX result");
    emulator.Step();
    True(emulator.CPU_Halted, "HLT state");
}

static void CpuRunStopsOnCancellation()
{
    Emulator emulator = NewEmulator(0xEA, 0xEA);
    using CancellationTokenSource cancellation = new();
    emulator.InstructionExecuted += (_, _, _) => cancellation.Cancel();
    emulator.Run(cancellation.Token);
    Equal((ushort)0x8001, emulator.ProgramCounter, "PC after cancellation between instructions");
}

static void CpuRecognizesOfficialOpcodes()
{
    byte[] officialOpcodes =
    {
        0x00,0x01,0x05,0x06,0x08,0x09,0x0A,0x0D,0x0E,
        0x10,0x11,0x15,0x16,0x18,0x19,0x1D,0x1E,
        0x20,0x21,0x24,0x25,0x26,0x28,0x29,0x2A,0x2C,0x2D,0x2E,
        0x30,0x31,0x35,0x36,0x38,0x39,0x3D,0x3E,
        0x40,0x41,0x45,0x46,0x48,0x49,0x4A,0x4C,0x4D,0x4E,
        0x50,0x51,0x55,0x56,0x58,0x59,0x5D,0x5E,
        0x60,0x61,0x65,0x66,0x68,0x69,0x6A,0x6C,0x6D,0x6E,
        0x70,0x71,0x75,0x76,0x78,0x79,0x7D,0x7E,
        0x81,0x84,0x85,0x86,0x88,0x8A,0x8C,0x8D,0x8E,
        0x90,0x91,0x94,0x95,0x96,0x98,0x99,0x9A,0x9D,
        0xA0,0xA1,0xA2,0xA4,0xA5,0xA6,0xA8,0xA9,0xAA,0xAC,0xAD,0xAE,
        0xB0,0xB1,0xB4,0xB5,0xB6,0xB8,0xB9,0xBA,0xBC,0xBD,0xBE,
        0xC0,0xC1,0xC4,0xC5,0xC6,0xC8,0xC9,0xCA,0xCC,0xCD,0xCE,
        0xD0,0xD1,0xD5,0xD6,0xD8,0xD9,0xDD,0xDE,
        0xE0,0xE1,0xE4,0xE5,0xE6,0xE8,0xE9,0xEA,0xEC,0xED,0xEE,
        0xF0,0xF1,0xF5,0xF6,0xF8,0xF9,0xFD,0xFE
    };

    Equal(151, officialOpcodes.Length, "official NMOS opcode count");
    foreach (byte opcode in officialOpcodes)
    {
        Emulator emulator = NewEmulator(opcode, 0x00, 0x80);
        string? diagnostic = null;
        emulator.Diagnostic += message => diagnostic = message;
        emulator.Step();
        True(diagnostic is null, $"official opcode 0x{opcode:X2} is dispatched");
        True(!emulator.CPU_Halted, $"official opcode 0x{opcode:X2} does not halt as unsupported");
    }
}

static void AdcIndirectYPageCrossTiming()
{
    Equal(5, RunAdcIndirectY(0x00FE), "ADC indirect,Y same-page cycles");
    Equal(6, RunAdcIndirectY(0x00FF), "ADC indirect,Y page-cross cycles");
}

static int RunAdcIndirectY(ushort baseAddress)
{
    Emulator emulator = NewEmulator(0x71, 0x20);
    emulator.Y = 1;
    emulator.RAM[0x20] = (byte)baseAddress;
    emulator.RAM[0x21] = (byte)(baseAddress >> 8);
    return emulator.Step();
}

static void IndexedReadPageCrossTiming()
{
    (byte Opcode, int Mode, int BaseCycles)[] cases =
    {
        (0xBD, 0, 4), (0xB9, 1, 4), (0xBE, 1, 4), (0xBC, 0, 4),
        (0x7D, 0, 4), (0x79, 1, 4), (0x71, 2, 5),
        (0xFD, 0, 4), (0xF9, 1, 4), (0xF1, 2, 5),
        (0x3D, 0, 4), (0x39, 1, 4), (0x31, 2, 5),
        (0x1D, 0, 4), (0x19, 1, 4), (0x11, 2, 5),
        (0x5D, 0, 4), (0x59, 1, 4), (0x51, 2, 5),
        (0xDD, 0, 4), (0xD9, 1, 4), (0xD1, 2, 5)
    };

    foreach ((byte opcode, int mode, int baseCycles) in cases)
    {
        Equal(baseCycles, ExecuteIndexedRead(opcode, mode, 0x00FE), $"{opcode:X2} same-page cycles");
        Equal(baseCycles + 1, ExecuteIndexedRead(opcode, mode, 0x00FF), $"{opcode:X2} page-cross cycles");
    }
}

static int ExecuteIndexedRead(byte opcode, int mode, ushort baseAddress)
{
    Emulator emulator = mode == 2 ? NewEmulator(opcode, 0x20) : NewEmulator(opcode, 0x00, 0x00);
    emulator.X = 1;
    emulator.Y = 1;
    if (mode == 2)
    {
        emulator.RAM[0x20] = (byte)baseAddress;
        emulator.RAM[0x21] = (byte)(baseAddress >> 8);
    }
    else
    {
        emulator.ROM[1] = (byte)baseAddress;
        emulator.ROM[2] = (byte)(baseAddress >> 8);
    }
    return emulator.Step();
}

static void BranchCycleTiming()
{
    byte[] branches = { 0x10, 0x30, 0x50, 0x70, 0x90, 0xB0, 0xD0, 0xF0 };
    foreach (byte opcode in branches)
    {
        Equal(2, ExecuteBranch(opcode, take: false, pc: 0x8000, offset: 1), $"{opcode:X2} not-taken cycles");
        Equal(3, ExecuteBranch(opcode, take: true, pc: 0x8000, offset: 1), $"{opcode:X2} same-page cycles");
        Equal(4, ExecuteBranch(opcode, take: true, pc: 0x80FD, offset: 1), $"{opcode:X2} forward page-cross cycles");
        Equal(4, ExecuteBranch(opcode, take: true, pc: 0x8100, offset: 0xFC), $"{opcode:X2} backward page-cross cycles");
    }
}

static int ExecuteBranch(byte opcode, bool take, ushort pc, byte offset)
{
    Emulator emulator = NewEmulator();
    emulator.ProgramCounter = pc;
    emulator.ROM[(pc - 0x8000) % 0x8000] = opcode;
    emulator.ROM[(pc - 0x8000 + 1) % 0x8000] = offset;
    emulator.flag_Negative = false;
    emulator.flag_Overflow = false;
    emulator.flag_Carry = false;
    emulator.flag_Zero = false;
    switch (opcode)
    {
        case 0x10: emulator.flag_Negative = !take; break;
        case 0x30: emulator.flag_Negative = take; break;
        case 0x50: emulator.flag_Overflow = !take; break;
        case 0x70: emulator.flag_Overflow = take; break;
        case 0x90: emulator.flag_Carry = !take; break;
        case 0xB0: emulator.flag_Carry = take; break;
        case 0xD0: emulator.flag_Zero = !take; break;
        case 0xF0: emulator.flag_Zero = take; break;
    }
    return emulator.Step();
}

static void CpuRamMirrors()
{
    Emulator emulator = new();
    emulator.Write(0x0005, 0xA5);
    Equal((byte)0xA5, emulator.Read(0x0805), "RAM mirror read");
    emulator.Write(0x1005, 0x3C);
    Equal((byte)0x3C, emulator.Read(0x0005), "RAM mirror write");
}

static void PpuDataReadsAreBuffered()
{
    Emulator emulator = new();
    SetPpuAddress(emulator, 0x2000);
    emulator.Write(0x2007, 0x42);
    SetPpuAddress(emulator, 0x2000);
    Equal((byte)0, emulator.Read(0x2007), "initial buffered read");
    Equal((byte)0x42, emulator.Read(0x2007), "buffered nametable read");
}

static void PpuPaletteEntriesMirror()
{
    Emulator emulator = new();
    SetPpuAddress(emulator, 0x3F10);
    emulator.Write(0x2007, 0x2A);
    SetPpuAddress(emulator, 0x3F00);
    Equal((byte)0x2A, emulator.Read(0x2007), "universal palette mirror");
}

static void PpuRaisesNmiAtVblank()
{
    Emulator emulator = NewEmulator(0xEA);
    emulator.Write(0x2000, 0x80);
    for (int i = 0; i < 16000 && !emulator.NMIRequested; i++) emulator.Step();
    True(emulator.NMIRequested, "vblank NMI request");
}

static void PpuNmiIsServiced()
{
    Emulator emulator = NewEmulator(0xEA);
    emulator.ROM[0x7FFA] = 0x34;
    emulator.ROM[0x7FFB] = 0x12;
    emulator.stackPointer = 0xFD;
    emulator.Write(0x2000, 0x80);
    for (int i = 0; i < 16000 && !emulator.NMIRequested; i++) emulator.Step();
    True(emulator.NMIRequested, "NMI pending at vblank");
    ushort returnPc = emulator.ProgramCounter;
    Equal(7, emulator.Step(), "NMI service cycles");
    Equal((ushort)0x1234, emulator.ProgramCounter, "NMI vector");
    Equal((byte)(returnPc >> 8), emulator.Read(0x01FD), "NMI stacked PC high");
    Equal((byte)returnPc, emulator.Read(0x01FC), "NMI stacked PC low");
    True((emulator.Read(0x01FB) & 0x30) == 0x20, "NMI stack status has U set and B clear");
    True(!emulator.NMIRequested, "NMI request cleared after service");
}

static void IrqIsMaskedAndServiced()
{
    Emulator emulator = NewEmulator(0xEA);
    emulator.ROM[0x7FFE] = 0x34;
    emulator.ROM[0x7FFF] = 0x12;
    emulator.stackPointer = 0xFD;
    emulator.SetIRQLine(true);
    emulator.flag_InterruptDisable = true;
    Equal(2, emulator.Step(), "masked IRQ executes next instruction");
    Equal((ushort)0x8001, emulator.ProgramCounter, "masked IRQ preserves PC flow");
    Equal((byte)0xFD, emulator.stackPointer, "masked IRQ does not push state");

    emulator.flag_InterruptDisable = false;
    emulator.flag_Carry = true;
    Equal(7, emulator.Step(), "IRQ service cycles");
    Equal((ushort)0x1234, emulator.ProgramCounter, "IRQ vector");
    Equal((byte)0x80, emulator.Read(0x01FD), "IRQ stacked PC high");
    Equal((byte)0x01, emulator.Read(0x01FC), "IRQ stacked PC low");
    Equal((byte)0x21, emulator.Read(0x01FB), "IRQ stack status has U set and B clear");
    True(emulator.flag_InterruptDisable, "IRQ sets interrupt mask");
    True(emulator.IRQRequested, "IRQ line remains asserted until the device releases it");
}

static void BrkPushesBreakStatus()
{
    Emulator emulator = NewEmulator(0x00, 0xEA);
    emulator.ROM[0x7FFE] = 0x00;
    emulator.ROM[0x7FFF] = 0x90;
    emulator.stackPointer = 0xFD;
    emulator.flag_InterruptDisable = false;
    emulator.flag_Carry = true;
    emulator.flag_Decimal = true;
    emulator.flag_Negative = true;
    Equal(7, emulator.Step(), "BRK cycles");
    Equal((ushort)0x9000, emulator.ProgramCounter, "BRK vector");
    Equal((byte)0x80, emulator.Read(0x01FD), "BRK stacked PC high");
    Equal((byte)0x02, emulator.Read(0x01FC), "BRK stacked PC after signature byte");
    Equal((byte)0xB9, emulator.Read(0x01FB), "BRK stack status includes B and U");
    True(emulator.flag_InterruptDisable, "BRK sets interrupt mask");
}

static void NromLoads()
{
    string path = Path.Combine(Path.GetTempPath(), $"yanesmu-{Guid.NewGuid():N}.nes");
    byte[] rom = new byte[16 + 0x4000];
    rom[0] = 0x4E;
    rom[1] = 0x45;
    rom[2] = 0x53;
    rom[3] = 0x1A;
    rom[4] = 1;
    rom[16] = 0xEA;
    rom[16 + 0x3FFC] = 0x00;
    rom[16 + 0x3FFD] = 0x80;
    File.WriteAllBytes(path, rom);
    try
    {
        Emulator emulator = new();
        True(emulator.Reset(path), "NROM load result");
        Equal((ushort)0x8000, emulator.ProgramCounter, "reset vector");
        Equal((byte)0xEA, emulator.Read(0x8000), "PRG bank mirror");
        SetPpuAddress(emulator, 0x0000);
        emulator.Write(0x2007, 0x5A);
        SetPpuAddress(emulator, 0x0000);
        emulator.Read(0x2007);
        Equal((byte)0x5A, emulator.Read(0x2007), "CHR RAM write");
    }
    finally
    {
        File.Delete(path);
    }
}

static void EmulatorPublishesFrames()
{
    Emulator emulator = new();
    int frames = 0;
    emulator.FrameReady += () => frames++;
    emulator.EmulatePPU();
    Equal(1, frames, "frame event count");
    Equal(256 * 240, emulator.GetFrameBuffer().Length, "framebuffer pixel count");
}

static void UnsupportedOpcodesPublishDiagnostics()
{
    Emulator emulator = NewEmulator(0xFF);
    string? diagnostic = null;
    emulator.Diagnostic += message => diagnostic = message;
    emulator.Step();
    True(emulator.CPU_Halted, "unsupported opcode halt");
    True(diagnostic?.Contains("Unknown opcode 0xFF", StringComparison.Ordinal) == true,
        "unsupported opcode diagnostic");
}

static Emulator NewEmulator(params byte[] program)
{
    Emulator emulator = new() { ProgramCounter = 0x8000 };
    Array.Fill(emulator.ROM, (byte)0xEA);
    Array.Copy(program, emulator.ROM, program.Length);
    return emulator;
}

static void SetPpuAddress(Emulator emulator, ushort address)
{
    emulator.Write(0x2006, (byte)(address >> 8));
    emulator.Write(0x2006, (byte)address);
}

static void True(bool value, string name)
{
    if (!value) throw new InvalidOperationException($"Expected true: {name}");
}

static void Equal<T>(T expected, T actual, string name) where T : IEquatable<T>
{
    if (!expected.Equals(actual))
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
}
