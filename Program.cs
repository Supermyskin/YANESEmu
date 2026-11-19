using System;
using System.IO;
using System.Diagnostics;
using System.Text;

public class Emulator
{
    public ushort ProgramCounter;
    public byte A;
    public byte X;
    public byte Y;
    public byte opcode;

    public bool writeLatch;
    public ushort vramAddr;
    public ushort tramAddr;
    private byte ppuControl;
    private byte ppuMask;
    private byte ppuStatus;
    private byte oamAddress;
    private byte ppuReadBuffer;
    private byte fineX;
    private byte scrollX;
    private byte scrollY;
    private int ppuScanline = 261;
    private int ppuDot;
    public bool NMIRequested { get; private set; }
    public bool TraceEnabled { get; set; }
    private bool verticalNametableMirroring;
    private bool chrRam;
    private readonly byte[] ppuRegisters = new byte[8];
    private readonly byte[] nametableRam = new byte[0x800];
    private readonly byte[] paletteRam = new byte[0x20];
    private readonly byte[] oam = new byte[0x100];

    public byte stackPointer;
    public ushort addressBus;
    public bool futureCarry;
    public long TotalCycles = 7;
    private bool pageCrossed;

    public bool CPU_Halted = false;

    public bool flag_Carry;
    public bool flag_Zero;
    public bool flag_InterruptDisable;
    public bool flag_Decimal;
    public bool flag_Overflow;
    public bool flag_Negative;

    String[] OpCodeNames =
	{
	"BRK","ORA","HLT","SLO","NOP","ORA","ASL","SLO","PHP","ORA","ASL","ANC","NOP","ORA","ASL","SLO",
	"BPL","ORA","HLT","SLO","NOP","ORA","ASL","SLO","CLC","ORA","NOP","SLO","NOP","ORA","ASL","SLO",
	"JSR","AND","HLT","RLA","BIT","AND","ROL","RLA","PLP","AND","ROL","ANC","BIT","AND","ROL","RLA",
	"BMI","AND","HLT","RLA","NOP","AND","ROL","RLA","SEC","AND","NOP","RLA","NOP","AND","ROL","RLA",
	"RTI","EOR","HLT","SRE","NOP","EOR","LSR","SRE","PHA","EOR","LSR","ALR","JMP","EOR","LSR","SRE",
	"BVC","EOR","HLT","SRE","NOP","EOR","LSR","SRE","CLI","EOR","NOP","SRE","NOP","EOR","LSR","SRE",
	"RTS","ADC","HLT","RRA","NOP","ADC","ROR","RRA","PLA","ADC","ROR","ARR","JMP","ADC","ROR","RRA",
	"BVS","ADC","HLT","RRA","NOP","ADC","ROR","RRA","SEI","ADC","NOP","RRA","NOP","ADC","ROR","RRA",
	"NOP","STA","NOP","SAX","STY","STA","STX","SAX","DEY","NOP","TXA","ANE","STY","STA","STX","SAX",
	"BCC","STA","HLT","SHA","STY","STA","STX","SAX","TYA","STA","TXS","SHS","SHY","STA","SHX","SHA",
	"LDY","LDA","LDX","LAX","LDY","LDA","LDX","LAX","TAY","LDA","TAX","LXA","LDY","LDA","LDX","LAX",
	"BCS","LDA","HLT","LAX","LDY","LDA","LDX","LAX","CLV","LDA","TSX","LAE","LDY","LDA","LDX","LAX",
	"CPY","CMP","NOP","DCP","CPY","CMP","DEC","DCP","INY","CMP","DEX","AXS","CPY","CMP","DEC","DCP",
	"BNE","CMP","HLT","DCP","NOP","CMP","DEC","DPC","CLD","CMP","NOP","DCP","NOP","CMP","DEC","DCP",
	"CPX","SBC","NOP","ISC","CPX","SBC","INC","ISC","INX","SBC","NOP","SBC","CPX","SBC","INC","ISC",
	"BEQ","SBC","HLT","ISC","NOP","SBC","INC","ISC","SED","SBC","NOP","ISC","NOP","SBC","INC","ISC",
    };

    public byte[] RAM = new byte[0x800];
    public byte[] ROM = new byte[0x8000];
    public byte[] CHRData = new byte[0x2000];
    public byte[] CHRROM = new byte[0x2000];

    private readonly uint[] frameBuffer = new uint[256 * 240];
    private long lastDisplayTicks;
    private bool terminalFrameStarted;
    private static readonly uint[] NesPalette =
    {
        0x666666,0x002A88,0x1412A7,0x3B00A4,0x5C007E,0x6E0040,0x6C0700,0x561D00,0x333500,0x0B4800,0x005200,0x004F08,0x00404D,0x000000,0x000000,0x000000,
        0xADADAD,0x155FD9,0x4240FF,0x7527FE,0xA01ACC,0xB71E7B,0xB53120,0x994E00,0x6B6D00,0x388700,0x0C9300,0x008F32,0x007C8D,0x000000,0x000000,0x000000,
        0xFFFEFF,0x64B0FF,0x9290FF,0xC676FF,0xF36AFF,0xFE6ECC,0xFE8170,0xEA9E22,0xBCBE00,0x88D800,0x5CE430,0x45E082,0x48CDDE,0x4F4F4F,0x000000,0x000000,
        0xFFFEFF,0xC0DFFF,0xD3D2FF,0xE8C8FF,0xFBC2FF,0xFEC4EA,0xFECCC5,0xF7D8A5,0xE4E594,0xCFEE96,0xBDF4AB,0xB3F3CC,0xB5EBF2,0xB8B8B8,0x000000,0x000000
    };

    public void Tracelogger(ushort pc, byte opcode)
    {
        if (!TraceEnabled) return;
        byte p = 0;
        if (flag_Carry)            p |= 0x01;
        if (flag_Zero)             p |= 0x02;
        if (flag_InterruptDisable) p |= 0x04;
        if (flag_Decimal)          p |= 0x08;
        p |= 0x20;
        if (flag_Overflow)         p |= 0x40;
        if (flag_Negative)         p |= 0x80;
        string flags = $"{(flag_Negative ? 'N' : 'n')}{(flag_Overflow ? 'V' : 'v')}--" +
            $"{(flag_Decimal ? 'D' : 'd')}{(flag_InterruptDisable ? 'I' : 'i')}" +
            $"{(flag_Zero ? 'Z' : 'z')}{(flag_Carry ? 'C' : 'c')}";
        byte b0 = opcode;
        byte b1 = Read((ushort)(pc + 1));
        byte b2 = Read((ushort)(pc + 2));
        string name = OpCodeNames[opcode];
        Console.WriteLine(
            $"{pc:X4}  {b0:X2} {b1:X2} {b2:X2}  {name,-4} " +
            $"\tA:{A:X2} X:{X:X2} Y:{Y:X2} P:{p:X2} {flags} SP:{stackPointer:X2} CYC:{TotalCycles,3}"
        );
    }

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

    private byte ReadPpuRegister(ushort register)
    {
        switch (register)
        {
            case 0x2002:
                byte status = (byte)((ppuStatus & 0xE0) | (ppuRegisters[2] & 0x1F));
                ppuStatus &= 0x7F;
                writeLatch = false;
                return status;
            case 0x2004:
                return oam[oamAddress];
            case 0x2007:
                ushort address = (ushort)(vramAddr & 0x3FFF);
                byte value = ReadPpuMemory(address);
                byte result = address >= 0x3F00 ? value : ppuReadBuffer;
                ppuReadBuffer = ReadPpuMemory((ushort)((address - (address >= 0x1000 ? 0x1000 : 0)) & 0x3FFF));
                IncrementVramAddress();
                return result;
            default:
                return ppuRegisters[register & 7];
        }
    }

    private void WritePpuRegister(ushort register, byte value)
    {
        ppuRegisters[register & 7] = value;
        switch (register)
        {
            case 0x2000:
                ppuControl = value;
                tramAddr = (ushort)((tramAddr & 0xF3FF) | ((value & 3) << 10));
                if ((ppuStatus & 0x80) != 0 && (ppuControl & 0x80) != 0) NMIRequested = true;
                break;
            case 0x2001:
                ppuMask = value;
                break;
            case 0x2003:
                oamAddress = value;
                break;
            case 0x2004:
                oam[oamAddress++] = value;
                break;
            case 0x2005:
                if (!writeLatch)
                {
                    scrollX = value;
                    fineX = (byte)(value & 7);
                    tramAddr = (ushort)((tramAddr & 0xFFE0) | (value >> 3));
                }
                else
                {
                    scrollY = value;
                    tramAddr = (ushort)((tramAddr & 0x8FFF) | ((value & 7) << 12));
                    tramAddr = (ushort)((tramAddr & 0xFC1F) | ((value & 0xF8) << 2));
                }
                writeLatch = !writeLatch;
                break;
            case 0x2006:
                if (!writeLatch)
                {
                    tramAddr = (ushort)((tramAddr & 0x00FF) | ((value & 0x3F) << 8));
                }
                else
                {
                    tramAddr = (ushort)((tramAddr & 0x7F00) | value);
                    vramAddr = tramAddr;
                }
                writeLatch = !writeLatch;
                break;
            case 0x2007:
                WritePpuMemory((ushort)(vramAddr & 0x3FFF), value);
                IncrementVramAddress();
                break;
        }
    }

    private void IncrementVramAddress()
    {
        vramAddr = (ushort)((vramAddr + ((ppuControl & 4) != 0 ? 32 : 1)) & 0x7FFF);
    }

    private byte ReadPpuMemory(ushort address)
    {
        address &= 0x3FFF;
        if (address < 0x2000)
        {
            return CHRData[address];
        }
        if (address < 0x3F00)
        {
            int index = (address - 0x2000) & 0x0FFF;
            int table = index >> 10;
            int offset = index & 0x3FF;
            int physicalTable = verticalNametableMirroring ? table & 1 : table >> 1;
            return nametableRam[physicalTable * 0x400 + offset];
        }
        return paletteRam[PaletteIndex(address)];
    }

    private void WritePpuMemory(ushort address, byte value)
    {
        address &= 0x3FFF;
        if (address < 0x2000)
        {
            if (chrRam) CHRData[address] = value;
            return;
        }
        if (address < 0x3F00)
        {
            int index = (address - 0x2000) & 0x0FFF;
            int table = index >> 10;
            int offset = index & 0x3FF;
            int physicalTable = verticalNametableMirroring ? table & 1 : table >> 1;
            nametableRam[physicalTable * 0x400 + offset] = value;
            return;
        }
        paletteRam[PaletteIndex(address)] = value;
    }

    private static int PaletteIndex(ushort address)
    {
        int index = (address - 0x3F00) & 0x1F;
        if ((index & 0x13) == 0x10) index -= 0x10;
        return index;
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

    //OPCode specific things

    void Op_LSR(ushort addr, byte input)
    {
        flag_Carry = (input & 0x01) != 0;
        byte result = (byte)(input >> 1);
        Write(addr, result);
        flag_Zero = result == 0;
        flag_Negative = false;
    }

    void ReadOperands_ZeroPageXAddressed()
    {
        addressBus = (ushort)((Read(ProgramCounter++) + X) & 0xFF);
    }

    void ReadOperands_ZeroPageYAddressed()
    {
        addressBus = (ushort)((Read(ProgramCounter++) + Y) & 0xFF);
    }

    void ReadOperands_AbsoluteXAddressed()
    {
        ushort baseAddr = ReadWord(ProgramCounter);
        ProgramCounter += 2;
        addressBus = (ushort)(baseAddr + X);
        pageCrossed = (baseAddr & 0xFF00) != (addressBus & 0xFF00);
    }

    void ReadOperands_AbsoluteYAddressed()
    {
        ushort baseAddr = ReadWord(ProgramCounter);
        ProgramCounter += 2;
        addressBus = (ushort)(baseAddr + Y);
        pageCrossed = (baseAddr & 0xFF00) != (addressBus & 0xFF00);
    }

    void ReadOperands_IndirectXAddressed()
    {
        byte zp = (byte)(Read(ProgramCounter++) + X);
        byte low = Read(zp);
        byte high = Read((ushort)((zp + 1) & 0xFF));
        addressBus = (ushort)((high << 8) | low);
    }

    void ReadOperands_IndirectYAddressed()
    {
        byte zp = Read(ProgramCounter++);
        byte low = Read(zp);
        byte high = Read((ushort)((zp + 1) & 0xFF));
        ushort baseAddr = (ushort)((high << 8) | low);
        addressBus = (ushort)(baseAddr + Y);
        pageCrossed = (baseAddr & 0xFF00) != (addressBus & 0xFF00);
    }

    private void SetZeroNegative(byte value)
    {
        flag_Zero = (value == 0);
        flag_Negative = (value & 0x80) != 0;
    }

    void Op_ASL(ushort Addr, byte Input)
    {
        flag_Carry = Input >= 0x80;
        Input <<= 1;
        Write(Addr, Input);
        flag_Negative = Input >= 0x80;
        flag_Zero = Input == 0x00;
    }

    void Op_ROL(ushort Addr, byte Input)
    {
        bool Futureflag_Carry = Input >= 0x80;
        Input <<= 1;
        if (flag_Carry)
        {
            Input |= 1;
        }
        Write(Addr, Input);
        flag_Carry = Futureflag_Carry;
        flag_Negative = Input >= 0x80;
        flag_Zero = Input == 0x00;
    }

    void Op_ROR(ushort addr, byte input)
    {
        futureCarry = (input & 0x01) != 0;
        byte result = (byte)(input >> 1);
        if (flag_Carry)
        {
            result |= 0x80;
        }
        Write(addr, result);
        flag_Carry = futureCarry;
        flag_Zero = result == 0;
        flag_Negative = result >= 0x80;
    }

    void ReadOperands_AbsoluteAdressed()
    {
        addressBus = Read(ProgramCounter);
        ProgramCounter++;
        addressBus = (ushort)(Read(ProgramCounter)<< 8 | addressBus);
        ProgramCounter++;
    }
    void ReadOperands_ZeroPageAddressed()
    {
        addressBus = Read(ProgramCounter);
        ProgramCounter++;
    }

    void Op_INC(ushort Addr, byte Input)
    {
        Input++;
        Write(Addr, Input);
        flag_Negative = Input >= 0x80;
        flag_Zero = Input == 0x00;
    }

    void Op_DEC(ushort Addr, byte Input)
    {
        Input--;
        Write(Addr, Input);
        flag_Negative = Input >= 0x80;
        flag_Zero = Input == 0x00;
    }

    void Op_ORA(byte Input)
    {
        A |= Input;
        flag_Negative = A >= 0x80;
        flag_Zero = A == 0x00;
    }

    void Op_AND(byte Input)
    {
        A &= Input;
        flag_Negative = A >= 0x80;
        flag_Zero = A == 0x00;
    }

    void Op_ADC(byte Input)
    {
        int sum = A + Input + (flag_Carry ? 1 : 0);
        flag_Overflow = (~(A ^ Input) & (A ^ sum) & 0x80) != 0;
        flag_Carry = sum > 0xFF;
        A = (byte)sum;
        SetZeroNegative(A);
    }

    void Op_CPX(byte input)
    {
        int temp = X - input;
        flag_Carry = X >= input;
        flag_Zero = (temp & 0xFF) == 0;
        flag_Negative = (temp & 0x80) != 0;
    }

    void Op_CPY(byte input)
    {
        int temp = Y - input;
        flag_Carry = Y >= input;
        flag_Zero = (temp & 0xFF) == 0;
        flag_Negative = (temp & 0x80) != 0;
    }

    void Op_SBC(byte Input)
    {
        Op_ADC((byte)~Input);
    }

    void Op_CMP(byte Input)
    {
        int temp = A - Input;
        flag_Carry = A >= Input;
        flag_Zero = (temp & 0xFF) == 0;
        flag_Negative = (temp & 0x80) != 0;
    }

    void Op_BIT(byte Input)
    {
        flag_Zero = (A & Input) == 0;
        flag_Negative = (Input & 0x80) != 0;
        flag_Overflow = (Input & 0x40) != 0;
    }

    void Op_EOR(byte Input)
    {
        A ^= Input;
        flag_Negative = A >= 0x80;
        flag_Zero = A == 0x00;
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

    private int FinishStep(int cycles)
    {
        TotalCycles += cycles;
        ClockPpu(cycles * 3);
        return cycles;
    }

    private void ClockPpu(int cycles)
    {
        for (int i = 0; i < cycles; i++)
        {
            ppuDot++;
            if (ppuScanline == 241 && ppuDot == 2)
            {
                ppuStatus |= 0x80;
                RenderBackground();
                if ((ppuControl & 0x80) != 0) NMIRequested = true;
            }
            else if (ppuScanline == 261 && ppuDot == 2)
            {
                ppuStatus &= 0x1F;
                NMIRequested = false;
            }

            if (ppuDot >= 341)
            {
                ppuDot = 0;
                ppuScanline++;
                if (ppuScanline >= 262) ppuScanline = 0;
            }
        }
    }

    private void RenderBackground()
    {
        if ((ppuMask & 0x08) == 0)
        {
            Array.Fill(frameBuffer, GetNesColor(ReadPpuMemory(0x3F00)));
            DisplayFrame();
            return;
        }

        int baseNametable = ppuControl & 3;
        int patternBase = (ppuControl & 0x10) != 0 ? 0x1000 : 0;
        for (int y = 0; y < 240; y++)
        {
            int worldY = (y + scrollY) % 480;
            int ntY = worldY / 240;
            int tileY = (worldY % 240) / 8;
            int pixelY = worldY & 7;
            for (int x = 0; x < 256; x++)
            {
                if (x < 8 && (ppuMask & 0x02) == 0)
                {
                    frameBuffer[y * 256 + x] = GetNesColor(ReadPpuMemory(0x3F00));
                    continue;
                }
                int worldX = (x + scrollX) % 512;
                int ntX = worldX / 256;
                int tileX = (worldX % 256) / 8;
                int pixelX = worldX & 7;
                int nametable = (baseNametable + ntY * 2 + ntX) & 3;
                ushort ntAddress = (ushort)(0x2000 + nametable * 0x400 + tileY * 32 + tileX);
                byte tile = ReadPpuMemory(ntAddress);
                byte low = ReadPpuMemory((ushort)(patternBase + tile * 16 + pixelY));
                byte high = ReadPpuMemory((ushort)(patternBase + tile * 16 + pixelY + 8));
                int colorIndex = ((low >> (7 - pixelX)) & 1) | (((high >> (7 - pixelX)) & 1) << 1);
                int palette = 0;
                if (colorIndex != 0)
                {
                    ushort attributeAddress = (ushort)(0x23C0 + nametable * 0x400 + (tileY / 4) * 8 + tileX / 4);
                    byte attribute = ReadPpuMemory(attributeAddress);
                    int shift = ((tileY & 2) << 1) | (tileX & 2);
                    palette = (attribute >> shift) & 3;
                }
                byte nesColor = ReadPpuMemory((ushort)(0x3F00 + palette * 4 + colorIndex));
                frameBuffer[y * 256 + x] = GetNesColor(nesColor);
            }
        }
        DisplayFrame();
    }

    private static uint GetNesColor(byte index)
    {
        return NesPalette[index & 0x3F];
    }

    private void DisplayFrame()
    {
        if (Console.IsOutputRedirected || TraceEnabled) return;
        long now = Stopwatch.GetTimestamp();
        if (lastDisplayTicks != 0 && Stopwatch.GetElapsedTime(lastDisplayTicks, now).TotalMilliseconds < 66) return;
        lastDisplayTicks = now;

        StringBuilder output = new(64 * 120 * 48);
        output.Append(terminalFrameStarted ? "\x1b[H" : "\x1b[2J\x1b[H");
        terminalFrameStarted = true;
        for (int y = 0; y < 240; y += 2)
        {
            for (int x = 0; x < 256; x += 4)
            {
                uint top = AveragePixels(y, x);
                uint bottom = AveragePixels(y + 1, x);
                output.Append("\x1b[38;2;").Append((top >> 16) & 255).Append(';').Append((top >> 8) & 255).Append(';').Append(top & 255);
                output.Append("m\x1b[48;2;").Append((bottom >> 16) & 255).Append(';').Append((bottom >> 8) & 255).Append(';').Append(bottom & 255);
                output.Append("m▀");
            }
            output.Append("\x1b[0m\n");
        }
        Console.Write(output.ToString());
    }

    private uint AveragePixels(int y, int x)
    {
        uint red = 0;
        uint green = 0;
        uint blue = 0;
        for (int i = 0; i < 4; i++)
        {
            uint color = frameBuffer[y * 256 + x + i];
            red += (color >> 16) & 255;
            green += (color >> 8) & 255;
            blue += color & 255;
        }
        return ((red / 4) << 16) | ((green / 4) << 8) | (blue / 4);
    }

    private void SaveFrame(string path)
    {
        const int width = 256;
        const int height = 240;
        const int rowSize = width * 3;
        int paddedRowSize = (rowSize + 3) & ~3;
        int imageSize = paddedRowSize * height;
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + imageSize);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                uint color = frameBuffer[y * width + x];
                writer.Write((byte)color);
                writer.Write((byte)(color >> 8));
                writer.Write((byte)(color >> 16));
            }
            for (int pad = rowSize; pad < paddedRowSize; pad++) writer.Write((byte)0);
        }
    }

    public int Step()
    {
        if (CPU_Halted) return FinishStep(0);

        if (NMIRequested)
        {
            Push((byte)(ProgramCounter >> 8));
            Push((byte)ProgramCounter);
            byte status = (byte)((flag_Carry ? 1 : 0) | (flag_Zero ? 2 : 0) |
                (flag_InterruptDisable ? 4 : 0) | (flag_Decimal ? 8 : 0) | 0x20 |
                (flag_Overflow ? 0x40 : 0) | (flag_Negative ? 0x80 : 0));
            Push(status);
            flag_InterruptDisable = true;
            ProgramCounter = ReadWord(0xFFFA);
            NMIRequested = false;
            return FinishStep(7);
        }

        ushort pcBeforeStep = ProgramCounter;
        opcode = Read(ProgramCounter++);
        ushort targetAddr;
        byte Temp_High;
        byte Temp_Low;
        byte Temp;

        Tracelogger(pcBeforeStep, opcode);

        switch (opcode)
        {
            case 0x02: // HLT
                CPU_Halted = true;
                return FinishStep(2);

            case 0xA0: // LDY Immediate
                Y = Read(ProgramCounter++);
                SetZeroNegative(Y);
                return FinishStep(2);

            case 0xA2: // LDX Immediate
                X = Read(ProgramCounter++);
                SetZeroNegative(X);
                return FinishStep(2);

            case 0xA9: // LDA Immediate
                A = Read(ProgramCounter++);
                SetZeroNegative(A);
                return FinishStep(2);

            case 0x85: // STA Zero Page
                targetAddr = Read(ProgramCounter++);
                Write(targetAddr, A);
                return FinishStep(3);

            case 0xA5: // LDA Zero Page
                targetAddr = Read(ProgramCounter++);
                A = Read(targetAddr);
                SetZeroNegative(A);
                return FinishStep(3);

            case 0x86: // STX Zero Page
                targetAddr = Read(ProgramCounter++);
                Write(targetAddr, X);
                return FinishStep(3);

            case 0x84: // STY Zero Page
                targetAddr = Read(ProgramCounter++);
                Write(targetAddr, Y);
                return FinishStep(3);

            case 0x8D: // STA Absolute
                targetAddr = ReadWord(ProgramCounter);
                ProgramCounter += 2;
                Write(targetAddr, A);
                return FinishStep(4);

            case 0xAD: // LDA Absolute
                targetAddr = ReadWord(ProgramCounter);
                ProgramCounter += 2;
                A = Read(targetAddr);
                SetZeroNegative(A);
                return FinishStep(4);

            case 0x8E: // STX Absolute
                targetAddr = ReadWord(ProgramCounter);
                ProgramCounter += 2;
                Write(targetAddr, X);
                return FinishStep(4);

            case 0x8C: // STY Absolute
                targetAddr = ReadWord(ProgramCounter);
                ProgramCounter += 2;
                Write(targetAddr, Y);
                return FinishStep(4);

            case 0x48: //PHA
                Push(A);
                return FinishStep(3);

            case 0x68: //PLA
                A = Pull();
                SetZeroNegative(A);
                return FinishStep(4);

            case 0x20: // JSR Absolute
                Temp_Low = Read(ProgramCounter++);
                Temp_High = Read(ProgramCounter);
                Push((byte)(ProgramCounter >> 8));
                Push((byte)(ProgramCounter & 0xFF));
                ProgramCounter = (ushort)((Temp_High << 8) | Temp_Low);
                return FinishStep(6);

            case 0x60: //RTS
                Temp_Low = Pull();
                Temp_High = Pull();
                ProgramCounter = (ushort)(Temp_High*256 + Temp_Low);
                ProgramCounter++;
                return FinishStep(6);

            case 0x4C: // JMP Absolute
                Temp_Low = Read(ProgramCounter);
                ProgramCounter++;
                Temp_High = Read(ProgramCounter);
                ProgramCounter = (ushort)((Temp_High * 256) + Temp_Low);
                return FinishStep(3);

            case 0xE8: // INX
                X++;
                SetZeroNegative(X);
                return FinishStep(2);

            case 0xCA: // DEX
                X--;
                SetZeroNegative(X);
                return FinishStep(2);

            case 0xC8: // INY
                Y++;
                SetZeroNegative(Y);
                return FinishStep(2);

            case 0x88: // DEY
                Y--;
                SetZeroNegative(Y);
                return FinishStep(2);

            case 0xAA: // TAX
                X = A;
                SetZeroNegative(X);
                return FinishStep(2);

            case 0x8A: // TXA
                A = X;
                SetZeroNegative(A);
                return FinishStep(2);

            case 0xA8: // TAY
                Y = A;
                SetZeroNegative(Y);
                return FinishStep(2);

            case 0x98: // TYA
                A = Y;
                SetZeroNegative(A);
                return FinishStep(2);

            case 0x9A: // TXS
                stackPointer = X;
                return FinishStep(2);

            case 0xBA: // TSX
                X = stackPointer;
                SetZeroNegative(X);
                return FinishStep(2);

            case 0x38: // SEC
                flag_Carry = true;
                return FinishStep(2);

            case 0x18: // CLC
                flag_Carry = false;
                return FinishStep(2);

            case 0xB8: // CLV
                flag_Overflow = false;
                return FinishStep(2);

            case 0x78: // SEI
                flag_InterruptDisable = true;
                return FinishStep(2);

            case 0x58: // CLI
                flag_InterruptDisable = false;
                return FinishStep(2);

            case 0xF8: // SED
                flag_Decimal = true;
                return FinishStep(2);

            case 0xD8: // CLD
                flag_Decimal = false;
                return FinishStep(2);

            case 0xEA: // NOP
                return FinishStep(2);

            case 0x08: //PHP
                Temp = 0;
                Temp += (byte)(flag_Carry ? 1 : 0);
                Temp += (byte)(flag_Zero ? 2 : 0);
                Temp += (byte)(flag_InterruptDisable ? 4 : 0);
                Temp += (byte)(flag_Decimal ? 8 : 0);
                Temp += 0x10;
                Temp += 0x20;
                Temp += (byte)(flag_Overflow ? 0x40 : 0);
                Temp += (byte)(flag_Negative ? 0x80 : 0);
                Push(Temp);
                return FinishStep(3);

            case 0x28: //PLP
                Temp = Pull();
                flag_Carry = (Temp & 1) != 0;
                flag_Zero = (Temp & 2) != 0;
                flag_InterruptDisable = (Temp & 4) != 0;
                flag_Decimal = (Temp & 8) != 0;
                flag_Overflow = (Temp & 0x40) != 0;
                flag_Negative = (Temp & 0x80) != 0;
                return FinishStep(4);

            case 0x0A: //ASL A
                flag_Carry = A > 127;
                A <<= 1;
                flag_Zero = A == 0;
                flag_Negative = A > 127;
                return FinishStep(2);

            case 0x06: //ASL Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_ASL(addressBus, Read(addressBus));
                return FinishStep(5);

            case 0x0E: //ASL Absolute
                ReadOperands_AbsoluteAdressed();
                Op_ASL(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0x2A: // ROL A
                bool futureCarry = (A >= 0x80);
                A <<= 1;
                if (flag_Carry)
                {
                    A |= 1;
                }
                flag_Carry = futureCarry;
                flag_Negative = (A >= 0x80);
                flag_Zero = (A == 0);
                return FinishStep(2);

            case 0x26: // ROL Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_ROL(addressBus, Read(addressBus));
                return FinishStep(5);

            case 0x2E: // ROL Absolute
                ReadOperands_AbsoluteAdressed();
                Op_ROL(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0x4A: // LSR A
                flag_Carry = (A & 0x01) != 0;
                A >>= 1;
                flag_Zero = (A == 0);
                flag_Negative = false;
                return FinishStep(2);

            case 0x46: // LSR Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_LSR(addressBus, Read(addressBus));
                return FinishStep(5);

            case 0x4E: // LSR Absolute
                ReadOperands_AbsoluteAdressed();
                Op_LSR(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0x6A: // ROR A
                futureCarry = (A & 0x01) != 0;
                A >>= 1;
                if (flag_Carry)
                {
                    A |= 0x80;
                }
                flag_Carry = futureCarry;
                flag_Zero = A == 0;
                flag_Negative = A >= 0x80;
                return FinishStep(2);

            case 0x66: // ROR Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_ROR(addressBus, Read(addressBus));
                return FinishStep(5);

            case 0x6E: // ROR Absolute
                ReadOperands_AbsoluteAdressed();
                Op_ROR(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0xE6: // INC Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_INC(addressBus, Read(addressBus));
                return FinishStep(5);

            case 0xEE: // INC Absolute
                ReadOperands_AbsoluteAdressed();
                Op_INC(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0xC6: // DEC Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_DEC(addressBus, Read(addressBus));
                return FinishStep(5);

            case 0xCE: // DEC Absolute
                ReadOperands_AbsoluteAdressed();
                Op_DEC(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0x09: // ORA Immediate
                Op_ORA(Read(ProgramCounter++));
                return FinishStep(2);

            case 0x05: // ORA Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_ORA(Read(addressBus));
                return FinishStep(3);

            case 0x0D: // ORA Absolute
                ReadOperands_AbsoluteAdressed();
                Op_ORA(Read(addressBus));
                return FinishStep(4);

            case 0x29: // AND Immediate
                Op_AND(Read(ProgramCounter++));
                return FinishStep(2);

            case 0x25: // AND Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_AND(Read(addressBus));
                return FinishStep(3);

            case 0x2D: // AND Absolute
                ReadOperands_AbsoluteAdressed();
                Op_AND(Read(addressBus));
                return FinishStep(4);

            case 0x49: // EOR Immediate
                Op_EOR(Read(ProgramCounter++));
                return FinishStep(2);

            case 0x45: // EOR Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_EOR(Read(addressBus));
                return FinishStep(3);

            case 0x4D: // EOR Absolute
                ReadOperands_AbsoluteAdressed();
                Op_EOR(Read(addressBus));
                return FinishStep(4);

            case 0x69: // ADC Immediate
                Op_ADC(Read(ProgramCounter++));
                return FinishStep(2);

            case 0x65: // ADC Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_ADC(Read(addressBus));
                return FinishStep(3);

            case 0x6D: // ADC Absolute
                ReadOperands_AbsoluteAdressed();
                Op_ADC(Read(addressBus));
                return FinishStep(4);

            case 0xE9: // SBC Immediate
                Op_SBC(Read(ProgramCounter++));
                return FinishStep(2);

            case 0xE5: // SBC Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_SBC(Read(addressBus));
                return FinishStep(3);

            case 0xED: // SBC Absolute
                ReadOperands_AbsoluteAdressed();
                Op_SBC(Read(addressBus));
                return FinishStep(4);

            case 0xC9: // CMP Immediate
                Op_CMP(Read(ProgramCounter++));
                return FinishStep(2);

            case 0xC5: // CMP Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_CMP(Read(addressBus));
                return FinishStep(3);

            case 0xCD: // CMP Absolute
                ReadOperands_AbsoluteAdressed();
                Op_CMP(Read(addressBus));
                return FinishStep(4);

            case 0x24: // BIT Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_BIT(Read(addressBus));
                return FinishStep(3);

            case 0x2C: // BIT Absolute
                ReadOperands_AbsoluteAdressed();
                Op_BIT(Read(addressBus));
                return FinishStep(4);


            case 0x89: // BIT Immediate (65C02 / Extended)
                ProgramCounter++;
                return FinishStep(2);


            case 0x00: // BRK Implied
                ProgramCounter++; // BRK advances PC by 1 extra byte padding
                Push((byte)(ProgramCounter >> 8));
                Push((byte)(ProgramCounter & 0xFF));

                // Push status register with Break flag (bit 4) set to 1
                byte brkFlags = 0;
                if (flag_Carry)            brkFlags |= 0x01;
                if (flag_Zero)             brkFlags |= 0x02;
                if (flag_InterruptDisable) brkFlags |= 0x04;
                if (flag_Decimal)          brkFlags |= 0x08;
                brkFlags |= 0x10; // Break flag (B flag) set when pushed by BRK
                brkFlags |= 0x20; // Unused bit always 1
                if (flag_Overflow)         brkFlags |= 0x40;
                if (flag_Negative)         brkFlags |= 0x80;
                Push(brkFlags);

                flag_InterruptDisable = true;

                // Jump to IRQ/BRK Vector ($FFFE/$FFFF)
                byte brkLow = Read(0xFFFE);
                byte brkHigh = Read(0xFFFF);
                ProgramCounter = (ushort)((brkHigh << 8) | brkLow);
                return FinishStep(7);


            case 0xE0: // CPX Immediate
                Op_CPX(Read(ProgramCounter++));
                return FinishStep(2);

            case 0xE4: // CPX Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_CPX(Read(addressBus));
                return FinishStep(3);

            case 0xEC: // CPX Absolute
                ReadOperands_AbsoluteAdressed();
                Op_CPX(Read(addressBus));
                return FinishStep(4);


            case 0xC0: // CPY Immediate
                Op_CPY(Read(ProgramCounter++));
                return FinishStep(2);

            case 0xC4: // CPY Zero Page
                ReadOperands_ZeroPageAddressed();
                Op_CPY(Read(addressBus));
                return FinishStep(3);

            case 0xCC: // CPY Absolute
                ReadOperands_AbsoluteAdressed();
                Op_CPY(Read(addressBus));
                return FinishStep(4);


            case 0x6C: // JMP Indirect ($XXXX)
                {
                    ushort ptr = ReadWord(ProgramCounter);
                    ProgramCounter += 2;

                    // Simulate 6502 Hardware Page Boundary Bug:
                    // If low byte of address is 0xFF, fetching high byte wraps around within the same page
                    ushort highPtr;
                    if ((ptr & 0x00FF) == 0x00FF)
                    {
                        highPtr = (ushort)(ptr & 0xFF00); // Wrap back to start of page
                    }
                    else
                    {
                        highPtr = (ushort)(ptr + 1);
                    }

                    byte jmpLow = Read(ptr);
                    byte jmpHigh = Read(highPtr);
                    ProgramCounter = (ushort)((jmpHigh << 8) | jmpLow);
                    return FinishStep(5);
                }


            case 0x1A: case 0x3A: case 0x5A: case 0x7A: case 0xDA: case 0xFA:
                return FinishStep(2);

            case 0x80: case 0x82: case 0xC2: case 0xE2: case 0x04: case 0x44: case 0x64:
                ProgramCounter++;
                return FinishStep(2);

            case 0x0C:
                ProgramCounter += 2;
                return FinishStep(4);

            case 0x1C: case 0x3C: case 0x5C: case 0x7C: case 0xDC: case 0xFC:
                ProgramCounter += 2;
                return FinishStep(4);

            case 0x40: // RTI Implied
            {
                byte status = Pull();
                flag_Carry = (status & 0x01) != 0;
                flag_Zero = (status & 0x02) != 0;
                flag_InterruptDisable = (status & 0x04) != 0;
                flag_Decimal = (status & 0x08) != 0;
                flag_Overflow = (status & 0x40) != 0;
                flag_Negative = (status & 0x80) != 0;
                byte rtiLow = Pull();
                byte rtiHigh = Pull();
                ProgramCounter = (ushort)((rtiHigh << 8) | rtiLow);
                return FinishStep(6);
            }

            case 0xB5: // LDA Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                A = Read(addressBus);
                SetZeroNegative(A);
                return FinishStep(4);

            case 0xBD: // LDA Absolute,X
                ReadOperands_AbsoluteXAddressed();
                A = Read(addressBus);
                SetZeroNegative(A);
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0xB9: // LDA Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                A = Read(addressBus);
                SetZeroNegative(A);
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0xA1: // LDA (Indirect,X)
                ReadOperands_IndirectXAddressed();
                A = Read(addressBus);
                SetZeroNegative(A);
                return FinishStep(6);

            case 0xB1: // LDA (Indirect),Y
                ReadOperands_IndirectYAddressed();
                A = Read(addressBus);
                SetZeroNegative(A);
                return FinishStep(5 + (pageCrossed ? 1 : 0));

            case 0xA6: // LDX Zero Page
                ReadOperands_ZeroPageAddressed();
                X = Read(addressBus);
                SetZeroNegative(X);
                return FinishStep(3);

            case 0xB6: // LDX Zero Page,Y
                ReadOperands_ZeroPageYAddressed();
                X = Read(addressBus);
                SetZeroNegative(X);
                return FinishStep(4);

            case 0xAE: // LDX Absolute
                ReadOperands_AbsoluteAdressed();
                X = Read(addressBus);
                SetZeroNegative(X);
                return FinishStep(4);

            case 0xBE: // LDX Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                X = Read(addressBus);
                SetZeroNegative(X);
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0xA4: // LDY Zero Page
                ReadOperands_ZeroPageAddressed();
                Y = Read(addressBus);
                SetZeroNegative(Y);
                return FinishStep(3);

            case 0xB4: // LDY Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Y = Read(addressBus);
                SetZeroNegative(Y);
                return FinishStep(4);

            case 0xAC: // LDY Absolute
                ReadOperands_AbsoluteAdressed();
                Y = Read(addressBus);
                SetZeroNegative(Y);
                return FinishStep(4);

            case 0xBC: // LDY Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Y = Read(addressBus);
                SetZeroNegative(Y);
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x95: // STA Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Write(addressBus, A);
                return FinishStep(4);

            case 0x9D: // STA Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Write(addressBus, A);
                return FinishStep(5);

            case 0x99: // STA Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                Write(addressBus, A);
                return FinishStep(5);

            case 0x81: // STA (Indirect,X)
                ReadOperands_IndirectXAddressed();
                Write(addressBus, A);
                return FinishStep(6);

            case 0x91: // STA (Indirect),Y
                ReadOperands_IndirectYAddressed();
                Write(addressBus, A);
                return FinishStep(6);

            case 0x96: // STX Zero Page,Y
                ReadOperands_ZeroPageYAddressed();
                Write(addressBus, X);
                return FinishStep(4);

            case 0x94: // STY Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Write(addressBus, Y);
                return FinishStep(4);

            case 0x75: // ADC Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_ADC(Read(addressBus));
                return FinishStep(4);

            case 0x7D: // ADC Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_ADC(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x79: // ADC Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                Op_ADC(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x61: // ADC (Indirect,X)
                ReadOperands_IndirectXAddressed();
                Op_ADC(Read(addressBus));
                return FinishStep(6);

            case 0x71: // ADC (Indirect),Y
                ReadOperands_IndirectYAddressed();
                Op_ADC(Read(addressBus));
                return FinishStep(5);

            case 0xF5: // SBC Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_SBC(Read(addressBus));
                return FinishStep(4);

            case 0xFD: // SBC Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_SBC(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0xF9: // SBC Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                Op_SBC(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0xE1: // SBC (Indirect,X)
                ReadOperands_IndirectXAddressed();
                Op_SBC(Read(addressBus));
                return FinishStep(6);

            case 0xF1: // SBC (Indirect),Y
                ReadOperands_IndirectYAddressed();
                Op_SBC(Read(addressBus));
                return FinishStep(5 + (pageCrossed ? 1 : 0));

            case 0x35: // AND Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_AND(Read(addressBus));
                return FinishStep(4);

            case 0x3D: // AND Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_AND(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x39: // AND Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                Op_AND(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x21: // AND (Indirect,X)
                ReadOperands_IndirectXAddressed();
                Op_AND(Read(addressBus));
                return FinishStep(6);

            case 0x31: // AND (Indirect),Y
                ReadOperands_IndirectYAddressed();
                Op_AND(Read(addressBus));
                return FinishStep(5 + (pageCrossed ? 1 : 0));

            case 0x15: // ORA Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_ORA(Read(addressBus));
                return FinishStep(4);

            case 0x1D: // ORA Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_ORA(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x19: // ORA Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                Op_ORA(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x01: // ORA (Indirect,X)
                ReadOperands_IndirectXAddressed();
                Op_ORA(Read(addressBus));
                return FinishStep(6);

            case 0x11: // ORA (Indirect),Y
                ReadOperands_IndirectYAddressed();
                Op_ORA(Read(addressBus));
                return FinishStep(5 + (pageCrossed ? 1 : 0));

            case 0x55: // EOR Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_EOR(Read(addressBus));
                return FinishStep(4);

            case 0x5D: // EOR Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_EOR(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x59: // EOR Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                Op_EOR(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0x41: // EOR (Indirect,X)
                ReadOperands_IndirectXAddressed();
                Op_EOR(Read(addressBus));
                return FinishStep(6);

            case 0x51: // EOR (Indirect),Y
                ReadOperands_IndirectYAddressed();
                Op_EOR(Read(addressBus));
                return FinishStep(5 + (pageCrossed ? 1 : 0));

            case 0xD5: // CMP Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_CMP(Read(addressBus));
                return FinishStep(4);

            case 0xDD: // CMP Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_CMP(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0xD9: // CMP Absolute,Y
                ReadOperands_AbsoluteYAddressed();
                Op_CMP(Read(addressBus));
                return FinishStep(4 + (pageCrossed ? 1 : 0));

            case 0xC1: // CMP (Indirect,X)
                ReadOperands_IndirectXAddressed();
                Op_CMP(Read(addressBus));
                return FinishStep(6);

            case 0xD1: // CMP (Indirect),Y
                ReadOperands_IndirectYAddressed();
                Op_CMP(Read(addressBus));
                return FinishStep(5 + (pageCrossed ? 1 : 0));

            case 0xF6: // INC Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_INC(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0xFE: // INC Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_INC(addressBus, Read(addressBus));
                return FinishStep(7);

            case 0xD6: // DEC Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_DEC(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0xDE: // DEC Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_DEC(addressBus, Read(addressBus));
                return FinishStep(7);

            case 0x16: // ASL Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_ASL(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0x1E: // ASL Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_ASL(addressBus, Read(addressBus));
                return FinishStep(7);

            case 0x56: // LSR Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_LSR(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0x5E: // LSR Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_LSR(addressBus, Read(addressBus));
                return FinishStep(7);

            case 0x36: // ROL Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_ROL(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0x3E: // ROL Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_ROL(addressBus, Read(addressBus));
                return FinishStep(7);

            case 0x76: // ROR Zero Page,X
                ReadOperands_ZeroPageXAddressed();
                Op_ROR(addressBus, Read(addressBus));
                return FinishStep(6);

            case 0x7E: // ROR Absolute,X
                ReadOperands_AbsoluteXAddressed();
                Op_ROR(addressBus, Read(addressBus));
                return FinishStep(7);

            // Branch Instuctions
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
                    return FinishStep(branchCycles);
                }

            default:
                Console.WriteLine($"Unknown opcode 0x{opcode:X2} at PC: 0x{(ProgramCounter - 1):X4}");
                CPU_Halted = true;
                return FinishStep(2);
        }

    }

    public void EmulatePPU()
    {
        RenderBackground();
    }

    public void Run()
    {
        while (!CPU_Halted)
        {
            Step();
        }
        RenderBackground();
        string framePath = Path.Combine(Directory.GetCurrentDirectory(), "frame.bmp");
        SaveFrame(framePath);
        Console.WriteLine($"Frame saved to {framePath}");
    }

    public bool Reset(string filepath)
    {
        if (!File.Exists(filepath))
        {
            Console.WriteLine($"Error: File not found at {filepath}");
            return false;
        }

        byte[] fileBytes = File.ReadAllBytes(filepath);

        if (fileBytes.Length < 16 ||
            fileBytes[0] != 0x4E ||
            fileBytes[1] != 0x45 ||
            fileBytes[2] != 0x53 ||
            fileBytes[3] != 0x1A)
        {
            Console.WriteLine("Error: Invalid iNES ROM file");
            return false;
        }

        int mapper = (fileBytes[6] >> 4) | (fileBytes[7] & 0xF0);
        if (mapper != 0 || (fileBytes[7] & 0x0C) == 0x08 ||
            fileBytes[4] is not (1 or 2) || fileBytes[5] > 1)
        {
            Console.WriteLine("Error: Only iNES NROM ROMs with 1–2 PRG banks and up to 1 CHR bank are supported");
            return false;
        }

        stackPointer = 0xFD;
        verticalNametableMirroring = (fileBytes[6] & 1) != 0;
        chrRam = fileBytes[5] == 0;
        Array.Clear(ppuRegisters, 0, ppuRegisters.Length);
        Array.Clear(nametableRam, 0, nametableRam.Length);
        Array.Clear(paletteRam, 0, paletteRam.Length);
        Array.Clear(oam, 0, oam.Length);
        ppuControl = 0;
        ppuMask = 0;
        ppuStatus = 0;
        oamAddress = 0;
        ppuReadBuffer = 0;
        fineX = 0;
        scrollX = 0;
        scrollY = 0;
        ppuScanline = 261;
        ppuDot = 0;
        NMIRequested = false;
        writeLatch = false;
        vramAddr = 0;
        tramAddr = 0;
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
            return false;
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

        TotalCycles = 7;

        if (ProgramCounter == 0x0000 || ProgramCounter == 0xFFFF)
        {
            ProgramCounter = 0x8000;
        }

        int chrPages = fileBytes[5];
        int chrSize = chrPages * 8192;
        int chrOffset = prgOffset + prgSize;
        Array.Clear(CHRData, 0, CHRData.Length);
        Array.Clear(CHRROM, 0, CHRROM.Length);

        if (chrPages > 0)
        {
            if (fileBytes.Length < chrOffset + chrSize)
            {
                Console.WriteLine("Error: ROM file is incomplete (CHR data is truncated)");
                return false;
            }

            int bytesToLoad = Math.Min(chrSize, CHRData.Length);
            Array.Copy(fileBytes, chrOffset, CHRData, 0, bytesToLoad);
            Array.Copy(CHRData, CHRROM, CHRData.Length);
        }

        return true;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Emulator emulator = new Emulator();
            emulator.TraceEnabled = Array.Exists(args, arg => arg == "--trace");
            string? romPath = Array.Find(args, arg => arg != "--trace");
            string path = romPath ?? Path.Combine(Directory.GetCurrentDirectory(), "7_Graphics.nes");

            if (!emulator.Reset(path))
            {
                Console.WriteLine("Usage: dotnet run -- <rom-file.nes> [--trace]");
                return;
            }
            Console.WriteLine($"Running {path}");
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
