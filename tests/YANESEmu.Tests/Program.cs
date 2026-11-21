using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

Run("CPU executes immediate load and transfer", CpuExecutesLoadAndTransfer);
Run("CPU run loop honors cancellation after the current instruction", CpuRunStopsOnCancellation);
Run("CPU recognizes all official NMOS opcodes with their base cycles", CpuRecognizesOfficialOpcodes);
Run("ADC indirect-indexed timing adds a page-cross cycle", AdcIndirectYPageCrossTiming);
Run("indexed reads account for page crossings", IndexedReadPageCrossTiming);
Run("conditional branches account for taken and page-cross cycles", BranchCycleTiming);
Run("common unofficial NOPs preserve state and use correct instruction lengths", CommonUnofficialNops);
Run("stable unofficial LAX and SAX operations execute", StableLaxAndSax);
Run("unofficial immediate SBC alias executes", UnofficialSbcAlias);
Run("ADC and SBC set binary result flags", CpuArithmeticFlags);
Run("BIT and compare instructions set their documented flags", CpuBitAndCompareFlags);
Run("zero-page indexed indirect modes wrap addresses", CpuZeroPageAddressWrapping);
Run("subroutine and interrupt returns restore the correct PC", CpuControlFlowReturns);
Run("CPU RAM mirrors every 0x800 bytes", CpuRamMirrors);
Run("PPU data reads are buffered", PpuDataReadsAreBuffered);
Run("PPU universal palette entries mirror", PpuPaletteEntriesMirror);
Run("PPU renders 8x8 sprites from OAM and CHR", PpuRendersSprites);
Run("PPU renders 8x16 sprites and reports scanline overflow", PpuRendersTallSpritesAndOverflow);
Run("PPU reproduces the diagonal sprite overflow scan", PpuReproducesOverflowBug);
Run("PPU detects opaque sprite zero overlap", PpuDetectsSpriteZeroHit);
Run("PPU raises NMI at vblank", PpuRaisesNmiAtVblank);
Run("reading PPU status clears the vblank NMI line", PpuStatusReadClearsNmi);
Run("NMI enters through its vector and pushes hardware status", PpuNmiIsServiced);
Run("IRQ obeys the interrupt mask and preserves the asserted line", IrqIsMaskedAndServiced);
Run("BRK pushes the software-interrupt status", BrkPushesBreakStatus);
Run("NROM cartridge loads reset vector and CHR RAM", NromLoads);
Run("CPU reset preserves the exact reset vector and initializes state", CpuResetState);
Run("emulator publishes frames without console output", EmulatorPublishesFrames);
Run("KIL opcodes publish diagnostics", KilOpcodesPublishDiagnostics);
RunOptionalNestest();
Console.WriteLine("All emulator checks passed.");

static void Run(string name, Action test)
{
    test();
    Console.WriteLine($"PASS {name}");
}

static void RunOptionalNestest()
{
    string? romPath = Environment.GetEnvironmentVariable("YANESEMU_NESTEST_ROM");
    string? logPath = Environment.GetEnvironmentVariable("YANESEMU_NESTEST_LOG");
    if (string.IsNullOrWhiteSpace(romPath) || string.IsNullOrWhiteSpace(logPath))
    {
        Console.WriteLine("SKIP nestest reference log (set YANESEMU_NESTEST_ROM and YANESEMU_NESTEST_LOG)");
        return;
    }

    Run("nestest matches the CPU reference log", () => NestestMatchesReferenceLog(romPath, logPath));
}

static void NestestMatchesReferenceLog(string romPath, string logPath)
{
    Emulator emulator = new();
    True(emulator.Reset(romPath), "nestest ROM loads");
    emulator.ProgramCounter = 0xC000; // nestest's automated CPU-test entry point

    Regex linePattern = new(@"^(?<pc>[0-9A-F]{4})\s+.*\bA:(?<a>[0-9A-F]{2})\s+X:(?<x>[0-9A-F]{2})\s+Y:(?<y>[0-9A-F]{2})\s+P:(?<p>[0-9A-F]{2})\s+SP:(?<sp>[0-9A-F]{2}).*\bCYC:(?<cyc>[0-9]+)", RegexOptions.Compiled);
    int lineNumber = 0;
    foreach (string line in File.ReadLines(logPath))
    {
        Match match = linePattern.Match(line);
        if (!match.Success) throw new InvalidOperationException($"nestest log line {lineNumber + 1} could not be parsed");
        string context = $"nestest line {lineNumber + 1}";
        Equal(Convert.ToUInt16(match.Groups["pc"].Value, 16), emulator.ProgramCounter, $"{context} PC");
        Equal(Convert.ToByte(match.Groups["a"].Value, 16), emulator.A, $"{context} A");
        Equal(Convert.ToByte(match.Groups["x"].Value, 16), emulator.X, $"{context} X");
        Equal(Convert.ToByte(match.Groups["y"].Value, 16), emulator.Y, $"{context} Y");
        Equal(Convert.ToByte(match.Groups["p"].Value, 16), CpuStatus(emulator), $"{context} P");
        Equal(Convert.ToByte(match.Groups["sp"].Value, 16), emulator.stackPointer, $"{context} SP");
        Equal(long.Parse(match.Groups["cyc"].Value, CultureInfo.InvariantCulture), emulator.TotalCycles, $"{context} cycle");
        emulator.Step();
        lineNumber++;
    }
    True(lineNumber > 8000, "nestest reference log ran through its CPU cases");
}

static byte CpuStatus(Emulator emulator)
{
    byte status = 0x20;
    if (emulator.flag_Carry) status |= 0x01;
    if (emulator.flag_Zero) status |= 0x02;
    if (emulator.flag_InterruptDisable) status |= 0x04;
    if (emulator.flag_Decimal) status |= 0x08;
    if (emulator.flag_Overflow) status |= 0x40;
    if (emulator.flag_Negative) status |= 0x80;
    return status;
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
    byte[] baseCycles =
    {
        7,6,0,0,0,3,5,0,3,2,2,0,0,4,6,0,
        2,5,0,0,0,4,6,0,2,4,2,0,0,4,7,0,
        6,6,0,0,3,3,5,0,4,2,2,0,4,4,6,0,
        2,5,0,0,0,4,6,0,2,4,2,0,0,4,7,0,
        6,6,0,0,0,3,5,0,3,2,2,0,3,4,6,0,
        2,5,0,0,0,4,6,0,2,4,2,0,0,4,7,0,
        6,6,0,0,0,3,5,0,4,2,2,0,5,4,6,0,
        2,5,0,0,0,4,6,0,2,4,2,0,0,4,7,0,
        0,6,0,0,3,3,3,0,2,2,2,0,4,4,4,0,
        2,6,0,0,4,4,4,0,2,5,2,0,0,5,0,0,
        2,6,2,0,3,3,3,0,2,2,2,0,4,4,4,0,
        2,5,0,0,4,4,4,0,2,4,2,0,4,4,4,0,
        2,6,0,0,3,3,5,0,2,2,2,0,4,4,6,0,
        2,5,0,0,0,4,6,0,2,4,2,0,0,4,7,0,
        2,6,0,0,3,3,5,0,2,2,2,0,4,4,6,0,
        2,5,0,0,0,4,6,0,2,4,2,0,0,4,7,0
    };
    Equal(256, baseCycles.Length, "opcode cycle table size");
    foreach (byte opcode in officialOpcodes)
    {
        Emulator emulator = NewEmulator(opcode, 0x00, 0x80);
        SetBranchNotTakenFlags(emulator, opcode);
        string? diagnostic = null;
        emulator.Diagnostic += message => diagnostic = message;
        Equal(baseCycles[opcode], (byte)emulator.Step(), $"official opcode 0x{opcode:X2} base cycles");
        True(diagnostic is null, $"official opcode 0x{opcode:X2} is dispatched");
        True(!emulator.CPU_Halted, $"official opcode 0x{opcode:X2} does not halt as unsupported");
    }
}

static void SetBranchNotTakenFlags(Emulator emulator, byte opcode)
{
    switch (opcode)
    {
        case 0x10: emulator.flag_Negative = true; break;
        case 0x30: emulator.flag_Negative = false; break;
        case 0x50: emulator.flag_Overflow = true; break;
        case 0x70: emulator.flag_Overflow = false; break;
        case 0x90: emulator.flag_Carry = true; break;
        case 0xB0: emulator.flag_Carry = false; break;
        case 0xD0: emulator.flag_Zero = true; break;
        case 0xF0: emulator.flag_Zero = false; break;
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

static void CommonUnofficialNops()
{
    Emulator immediate = NewEmulator(0x89, 0x55);
    immediate.A = 0xA5;
    immediate.flag_Carry = true;
    Equal(2, immediate.Step(), "immediate NOP cycles");
    Equal((ushort)0x8002, immediate.ProgramCounter, "immediate NOP length");
    Equal((byte)0xA5, immediate.A, "immediate NOP preserves A");
    True(immediate.flag_Carry, "immediate NOP preserves flags");

    Emulator zeroPage = NewEmulator(0x04, 0x10);
    Equal(3, zeroPage.Step(), "zero-page NOP cycles");
    Equal((ushort)0x8002, zeroPage.ProgramCounter, "zero-page NOP length");

    Emulator zeroPageX = NewEmulator(0x14, 0xFF);
    zeroPageX.X = 1;
    Equal(4, zeroPageX.Step(), "zero-page,X NOP cycles");
    Equal((ushort)0x8002, zeroPageX.ProgramCounter, "zero-page,X NOP length");

    Emulator absolute = NewEmulator(0x0C, 0x00, 0x10);
    Equal(4, absolute.Step(), "absolute NOP cycles");
    Equal((ushort)0x8003, absolute.ProgramCounter, "absolute NOP length");

    Emulator absoluteX = NewEmulator(0x1C, 0xFF, 0x00);
    absoluteX.X = 1;
    Equal(5, absoluteX.Step(), "absolute,X NOP page-cross cycles");
    Equal((ushort)0x8003, absoluteX.ProgramCounter, "absolute,X NOP length");

    Emulator kil = NewEmulator(0x12);
    string? diagnostic = null;
    kil.Diagnostic += message => diagnostic = message;
    Equal(2, kil.Step(), "KIL cycles");
    True(kil.CPU_Halted, "KIL halts the processor");
    True(diagnostic?.Contains("KIL opcode 0x12", StringComparison.Ordinal) == true, "KIL diagnostic");
}

static void StableLaxAndSax()
{
    Emulator lax = NewEmulator(0xA7, 0x10);
    lax.RAM[0x10] = 0x80;
    Equal(3, lax.Step(), "LAX zero-page cycles");
    Equal((byte)0x80, lax.A, "LAX loads A");
    Equal((byte)0x80, lax.X, "LAX loads X");
    True(lax.flag_Negative && !lax.flag_Zero, "LAX flags");

    Emulator sax = NewEmulator(0x87, 0x20);
    sax.A = 0xF0;
    sax.X = 0x3C;
    sax.flag_Carry = true;
    Equal(3, sax.Step(), "SAX zero-page cycles");
    Equal((byte)0x30, sax.RAM[0x20], "SAX stores A AND X");
    True(sax.flag_Carry, "SAX preserves flags");
}

static void UnofficialSbcAlias()
{
    Emulator emulator = NewEmulator(0xEB, 0x01);
    emulator.A = 0x02;
    emulator.flag_Carry = true;
    Equal(2, emulator.Step(), "unofficial SBC cycles");
    Equal((byte)0x01, emulator.A, "unofficial SBC result");
    True(emulator.flag_Carry, "unofficial SBC carry");
}

static void CpuArithmeticFlags()
{
    Emulator adcOverflow = NewEmulator(0x69, 0x50);
    adcOverflow.A = 0x50;
    Equal(2, adcOverflow.Step(), "ADC immediate cycles");
    Equal((byte)0xA0, adcOverflow.A, "ADC overflow result");
    True(adcOverflow.flag_Overflow && adcOverflow.flag_Negative && !adcOverflow.flag_Carry,
        "ADC signed overflow flags");

    Emulator adcCarry = NewEmulator(0x69, 0x00);
    adcCarry.A = 0xFF;
    adcCarry.flag_Carry = true;
    adcCarry.flag_Decimal = true;
    adcCarry.Step();
    Equal((byte)0x00, adcCarry.A, "NES ADC ignores decimal mode");
    True(adcCarry.flag_Carry && adcCarry.flag_Zero && !adcCarry.flag_Overflow,
        "ADC carry and zero flags");

    Emulator sbcOverflow = NewEmulator(0xE9, 0x01);
    sbcOverflow.A = 0x80;
    sbcOverflow.flag_Carry = true;
    sbcOverflow.Step();
    Equal((byte)0x7F, sbcOverflow.A, "SBC result");
    True(sbcOverflow.flag_Carry && sbcOverflow.flag_Overflow && !sbcOverflow.flag_Negative,
        "SBC no-borrow and overflow flags");

    Emulator sbcBorrow = NewEmulator(0xE9, 0x01);
    sbcBorrow.A = 0;
    sbcBorrow.flag_Carry = true;
    sbcBorrow.Step();
    Equal((byte)0xFF, sbcBorrow.A, "SBC borrow result");
    True(!sbcBorrow.flag_Carry && sbcBorrow.flag_Negative,
        "SBC borrow and negative flags");
}

static void CpuBitAndCompareFlags()
{
    Emulator bit = NewEmulator(0x24, 0x10);
    bit.A = 0x40;
    bit.RAM[0x10] = 0xC0;
    bit.flag_Carry = true;
    bit.Step();
    True(!bit.flag_Zero && bit.flag_Negative && bit.flag_Overflow && bit.flag_Carry,
        "BIT copies N/V, computes Z, and preserves C");

    Emulator cmpEqual = NewEmulator(0xC9, 0x7F);
    cmpEqual.A = 0x7F;
    cmpEqual.Step();
    True(cmpEqual.flag_Carry && cmpEqual.flag_Zero && !cmpEqual.flag_Negative,
        "CMP equal flags");

    Emulator cmpLess = NewEmulator(0xE0, 0x01);
    cmpLess.X = 0;
    cmpLess.Step();
    True(!cmpLess.flag_Carry && !cmpLess.flag_Zero && cmpLess.flag_Negative,
        "CPX lower-than flags");

    Emulator cpyGreater = NewEmulator(0xC4, 0x01);
    cpyGreater.Y = 2;
    cpyGreater.RAM[1] = 1;
    cpyGreater.Step();
    True(cpyGreater.flag_Carry && !cpyGreater.flag_Zero && !cpyGreater.flag_Negative,
        "CPY greater-than flags");
}

static void CpuZeroPageAddressWrapping()
{
    Emulator indexed = NewEmulator(0xB4, 0xFF); // LDY $FF,X
    indexed.X = 1;
    indexed.RAM[0] = 0x80;
    Equal(4, indexed.Step(), "zero-page indexed load cycles");
    Equal((byte)0x80, indexed.Y, "zero-page indexed address wraps");

    Emulator indirectX = NewEmulator(0xA1, 0xFF); // LDA ($FF,X)
    indirectX.X = 1;
    indirectX.RAM[0] = 0x34;
    indirectX.RAM[1] = 0x12;
    indirectX.RAM[0x234] = 0xA9;
    Equal(6, indirectX.Step(), "indirect,X cycles");
    Equal((byte)0xA9, indirectX.A, "indirect,X pointer wraps in zero page");

    Emulator indirectY = NewEmulator(0xB1, 0xFF); // LDA ($FF),Y
    indirectY.Y = 1;
    indirectY.RAM[0xFF] = 0xFF;
    indirectY.RAM[0] = 0x00;
    indirectY.RAM[0x100] = 0x7F;
    Equal(6, indirectY.Step(), "indirect,Y page-cross cycles");
    Equal((byte)0x7F, indirectY.A, "indirect,Y pointer wraps in zero page");
}

static void CpuControlFlowReturns()
{
    Emulator subroutine = NewEmulator(0x20, 0x00, 0x90);
    subroutine.stackPointer = 0xFD;
    Equal(6, subroutine.Step(), "JSR cycles");
    Equal((ushort)0x9000, subroutine.ProgramCounter, "JSR target");
    Equal((byte)0x80, subroutine.Read(0x01FD), "JSR stacked PC high");
    Equal((byte)0x02, subroutine.Read(0x01FC), "JSR stacked return address");
    subroutine.ROM[0x1000] = 0x60;
    Equal(6, subroutine.Step(), "RTS cycles");
    Equal((ushort)0x8003, subroutine.ProgramCounter, "RTS resumes after JSR operands");

    Emulator indirectJump = NewEmulator(0x6C, 0xFF, 0x00);
    indirectJump.RAM[0xFF] = 0x34;
    indirectJump.RAM[0x00] = 0x12;
    indirectJump.RAM[0x100] = 0x56;
    Equal(5, indirectJump.Step(), "indirect JMP cycles");
    Equal((ushort)0x1234, indirectJump.ProgramCounter, "indirect JMP page-boundary hardware wrap");

    Emulator rti = NewEmulator(0x40);
    rti.stackPointer = 0xFA;
    rti.RAM[0x1FB] = 0xCB;
    rti.RAM[0x1FC] = 0x34;
    rti.RAM[0x1FD] = 0x12;
    Equal(6, rti.Step(), "RTI cycles");
    Equal((ushort)0x1234, rti.ProgramCounter, "RTI restored PC");
    True(rti.flag_Carry && rti.flag_Zero && rti.flag_Decimal && rti.flag_Overflow && rti.flag_Negative,
        "RTI restored processor flags");
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

static void PpuRendersSprites()
{
    Emulator emulator = new();
    SetPpuAddress(emulator, 0x0010);
    emulator.Write(0x2007, 0xFF); // tile 1, first row, low bit plane
    SetPpuAddress(emulator, 0x3F11);
    emulator.Write(0x2007, 0x01);
    emulator.Write(0x2003, 0);
    emulator.Write(0x2004, 0);  // top is OAM Y + 1
    emulator.Write(0x2004, 1);
    emulator.Write(0x2004, 0);
    emulator.Write(0x2004, 10);
    emulator.Write(0x2001, 0x14); // sprites and left-edge sprites enabled
    emulator.EmulatePPU();

    uint[] frame = emulator.GetFrameBuffer();
    Equal(0x002A88u, frame[1 * 256 + 10], "opaque sprite pixel");
    Equal(0u, frame[1 * 256 + 9], "transparent pixel remains backdrop");
}

static void PpuRendersTallSpritesAndOverflow()
{
    Emulator emulator = new();
    SetPpuAddress(emulator, 0x0030); // 8x16 tile 2, second tile row (tile 3)
    emulator.Write(0x2007, 0x80);
    SetPpuAddress(emulator, 0x3F11);
    emulator.Write(0x2007, 0x01);
    emulator.Write(0x2000, 0x20); // select 8x16 sprites
    emulator.Write(0x2003, 0);
    for (int sprite = 0; sprite < 9; sprite++)
    {
        emulator.Write(0x2004, 0);
        emulator.Write(0x2004, 2);
        emulator.Write(0x2004, 0);
        emulator.Write(0x2004, (byte)(sprite * 8));
    }
    emulator.Write(0x2001, 0x14);
    emulator.EmulatePPU();

    Equal(0x002A88u, emulator.GetFrameBuffer()[9 * 256], "lower tile in 8x16 sprite");
    True((emulator.Read(0x2002) & 0x20) != 0, "sprite overflow status");
}

static void PpuDetectsSpriteZeroHit()
{
    Emulator emulator = new();
    SetPpuAddress(emulator, 0x0000);
    emulator.Write(0x2007, 0x80); // background tile 0, first pixel
    SetPpuAddress(emulator, 0x0001);
    emulator.Write(0x2007, 0x80); // background tile 0, second row
    SetPpuAddress(emulator, 0x0010);
    emulator.Write(0x2007, 0x80); // sprite tile 1, first pixel
    SetPpuAddress(emulator, 0x3F01);
    emulator.Write(0x2007, 0x02);
    SetPpuAddress(emulator, 0x3F11);
    emulator.Write(0x2007, 0x01);
    emulator.Write(0x2003, 0);
    emulator.Write(0x2004, 0);
    emulator.Write(0x2004, 1);
    emulator.Write(0x2004, 0);
    emulator.Write(0x2004, 0);
    emulator.Write(0x2001, 0x1E); // render both layers, including their left edges
    emulator.EmulatePPU();

    True((emulator.Read(0x2002) & 0x40) != 0, "sprite zero hit status");
}

static void PpuReproducesOverflowBug()
{
    Emulator emulator = new();
    emulator.Write(0x2003, 0);
    for (int sprite = 0; sprite < 64; sprite++)
    {
        byte y = sprite < 8 ? (byte)0xFF : (byte)30;
        emulator.Write(0x2004, y);
        emulator.Write(0x2004, sprite == 9 ? (byte)0xFF : (byte)0);
        emulator.Write(0x2004, 0);
        emulator.Write(0x2004, 0);
    }
    emulator.Write(0x2001, 0x10);
    emulator.EmulatePPU();
    True((emulator.Read(0x2002) & 0x20) != 0, "non-Y OAM byte triggers diagonal overflow scan");
}

static void PpuRaisesNmiAtVblank()
{
    Emulator emulator = NewEmulator(0xEA);
    emulator.Write(0x2000, 0x80);
    for (int i = 0; i < 16000 && !emulator.NMIRequested; i++) emulator.Step();
    True(emulator.NMIRequested, "vblank NMI request");
}

static void PpuStatusReadClearsNmi()
{
    Emulator emulator = NewEmulator(0xEA);
    emulator.Write(0x2000, 0x80);
    for (int i = 0; i < 16000 && !emulator.NMIRequested; i++) emulator.Step();
    True(emulator.NMIRequested, "vblank NMI asserted before status read");
    emulator.Read(0x2002);
    True(!emulator.NMIRequested, "status read deasserts vblank NMI");
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
        emulator.A = emulator.X = emulator.Y = 0xFF;
        emulator.stackPointer = 0;
        emulator.CPU_Halted = true;
        emulator.flag_Carry = emulator.flag_Zero = emulator.flag_Decimal = emulator.flag_Overflow = emulator.flag_Negative = true;
        emulator.flag_InterruptDisable = false;
        emulator.SetIRQLine(true);
        True(emulator.Reset(path), "NROM load result");
        Equal((ushort)0x8000, emulator.ProgramCounter, "reset vector");
        Equal((byte)0, emulator.A, "reset A");
        Equal((byte)0, emulator.X, "reset X");
        Equal((byte)0, emulator.Y, "reset Y");
        Equal((byte)0xFD, emulator.stackPointer, "reset stack pointer");
        True(!emulator.CPU_Halted, "reset clears halt");
        True(!emulator.flag_Carry && !emulator.flag_Zero && emulator.flag_InterruptDisable &&
            !emulator.flag_Decimal && !emulator.flag_Overflow && !emulator.flag_Negative,
            "reset processor flags");
        True(!emulator.IRQRequested, "reset clears IRQ line state");
        Equal(7L, emulator.TotalCycles, "reset cycle count");
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

static void CpuResetState()
{
    foreach (ushort vector in new ushort[] { 0x0000, 0xFFFF })
    {
        string path = Path.Combine(Path.GetTempPath(), $"yanesmu-reset-{Guid.NewGuid():N}.nes");
        byte[] rom = new byte[16 + 0x4000];
        rom[0] = 0x4E;
        rom[1] = 0x45;
        rom[2] = 0x53;
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[16 + 0x3FFC] = (byte)vector;
        rom[16 + 0x3FFD] = (byte)(vector >> 8);
        File.WriteAllBytes(path, rom);
        try
        {
            Emulator emulator = new();
            True(emulator.Reset(path), $"ROM with reset vector {vector:X4} loads");
            Equal(vector, emulator.ProgramCounter, $"exact reset vector {vector:X4}");
        }
        finally
        {
            File.Delete(path);
        }
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

static void KilOpcodesPublishDiagnostics()
{
    Emulator emulator = NewEmulator(0x02);
    string? diagnostic = null;
    emulator.Diagnostic += message => diagnostic = message;
    emulator.Step();
    True(emulator.CPU_Halted, "unsupported opcode halt");
    True(diagnostic?.Contains("Unofficial KIL opcode 0x02", StringComparison.Ordinal) == true,
        "KIL diagnostic");
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
