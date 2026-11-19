using System;
using System.IO;

Run("CPU executes immediate load and transfer", CpuExecutesLoadAndTransfer);
Run("CPU RAM mirrors every 0x800 bytes", CpuRamMirrors);
Run("PPU data reads are buffered", PpuDataReadsAreBuffered);
Run("PPU universal palette entries mirror", PpuPaletteEntriesMirror);
Run("PPU raises NMI at vblank", PpuRaisesNmiAtVblank);
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
