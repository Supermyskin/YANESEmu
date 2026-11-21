using System;

public partial class Emulator
{
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
    private bool verticalNametableMirroring;
    private bool chrRam;
    private readonly byte[] ppuRegisters = new byte[8];
    private readonly byte[] nametableRam = new byte[0x800];
    private readonly byte[] paletteRam = new byte[0x20];
    private readonly byte[] oam = new byte[0x100];

    private readonly uint[] frameBuffer = new uint[256 * 240];
    private static readonly uint[] NesPalette =
    {
        0x666666,0x002A88,0x1412A7,0x3B00A4,0x5C007E,0x6E0040,0x6C0700,0x561D00,0x333500,0x0B4800,0x005200,0x004F08,0x00404D,0x000000,0x000000,0x000000,
        0xADADAD,0x155FD9,0x4240FF,0x7527FE,0xA01ACC,0xB71E7B,0xB53120,0x994E00,0x6B6D00,0x388700,0x0C9300,0x008F32,0x007C8D,0x000000,0x000000,0x000000,
        0xFFFEFF,0x64B0FF,0x9290FF,0xC676FF,0xF36AFF,0xFE6ECC,0xFE8170,0xEA9E22,0xBCBE00,0x88D800,0x5CE430,0x45E082,0x48CDDE,0x4F4F4F,0x000000,0x000000,
        0xFFFEFF,0xC0DFFF,0xD3D2FF,0xE8C8FF,0xFBC2FF,0xFEC4EA,0xFECCC5,0xF7D8A5,0xE4E594,0xCFEE96,0xBDF4AB,0xB3F3CC,0xB5EBF2,0xB8B8B8,0x000000,0x000000
    };

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
                ppuReadBuffer = ReadPpuMemory((ushort)((address - (address >= 0x3F00 ? 0x1000 : 0)) & 0x3FFF));
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
        byte[] backgroundPixels = new byte[frameBuffer.Length];
        byte backdrop = ReadPpuMemory(0x3F00);
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
                int pixelIndex = y * 256 + x;
                if ((ppuMask & 0x08) == 0 || (x < 8 && (ppuMask & 0x02) == 0))
                {
                    frameBuffer[pixelIndex] = GetNesColor(backdrop);
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
                backgroundPixels[pixelIndex] = (byte)colorIndex;
                frameBuffer[pixelIndex] = GetNesColor(nesColor);
            }
        }

        RenderSprites(backgroundPixels, backdrop);
        PublishFrame();
    }

    private void RenderSprites(byte[] backgroundPixels, byte backdrop)
    {
        if ((ppuMask & 0x10) == 0) return;

        for (int y = 0; y < 240; y++)
        {
            for (int x = (ppuMask & 0x04) != 0 ? 0 : 8; x < 256; x++)
            {
                int frameIndex = y * 256 + x;
                for (int sprite = 0; sprite < 64; sprite++)
                {
                    int oamIndex = sprite * 4;
                    int top = oam[oamIndex] + 1;
                    int left = oam[oamIndex + 3];
                    int row = y - top;
                    int column = x - left;
                    if ((uint)row >= 8 || (uint)column >= 8) continue;

                    byte tile = oam[oamIndex + 1];
                    byte attributes = oam[oamIndex + 2];
                    if ((attributes & 0x80) != 0) row = 7 - row;
                    if ((attributes & 0x40) != 0) column = 7 - column;
                    int pattern = ((ppuControl & 0x08) != 0 ? 0x1000 : 0) + tile * 16;
                    int bit = 7 - column;
                    int color = ((ReadPpuMemory((ushort)(pattern + row)) >> bit) & 1)
                        | (((ReadPpuMemory((ushort)(pattern + row + 8)) >> bit) & 1) << 1);
                    if (color == 0) continue;

                    if (sprite == 0 && backgroundPixels[frameIndex] != 0 && x < 255
                        && (ppuMask & 0x18) == 0x18)
                        ppuStatus |= 0x40;

                    if ((attributes & 0x20) == 0 || backgroundPixels[frameIndex] == 0)
                    {
                        int palette = attributes & 3;
                        byte nesColor = ReadPpuMemory((ushort)(0x3F10 + palette * 4 + color));
                        frameBuffer[frameIndex] = GetNesColor(nesColor);
                    }
                    break;
                }
            }
        }
    }

    private static uint GetNesColor(byte index)
    {
        return NesPalette[index & 0x3F];
    }

    private uint[] CopyFrameBuffer() => (uint[])frameBuffer.Clone();

    private void PublishFrame() => FrameReady?.Invoke();

    public void EmulatePPU() => RenderBackground();
}
