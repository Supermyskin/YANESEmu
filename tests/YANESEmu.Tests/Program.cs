using System;
using System.IO;
using System.Threading;

Run("CPU executes immediate load and transfer", CpuExecutesLoadAndTransfer);
Run("CPU run loop honors cancellation after the current instruction", CpuRunStopsOnCancellation);
Run("CPU recognizes every official NMOS 6502 opcode", CpuRecognizesOfficialOpcodes);
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
