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
    private bool oddFrame;
    public bool NMIRequested { get; private set; }
    private bool verticalNametableMirroring;
    private bool chrRam = true;
    private readonly byte[] ppuRegisters = new byte[8];
    private readonly byte[] nametableRam = new byte[0x800];
    private readonly byte[] paletteRam = new byte[0x20];
    private readonly byte[] oam = new byte[0x100];

    private readonly uint[] frameBuffer = new uint[256 * 240];
    private readonly byte[] backgroundPixels = new byte[256 * 240];
    private readonly int[] scanlineSprites = new int[8];
    private int scanlineSpriteCount;
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
                if (ppuScanline == 241 && ppuDot <= 2)
                    ppuStatus &= 0x7F;
                byte status = (byte)((ppuStatus & 0xE0) | (ppuRegisters[2] & 0x1F));
                ppuStatus &= 0x7F;
                NMIRequested = false;
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
                NMIRequested = (ppuStatus & 0x80) != 0 && (ppuControl & 0x80) != 0;
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

    private void WriteOamDmaByte(byte value) => oam[oamAddress++] = value;

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
                NMIRequested = (ppuControl & 0x80) != 0;
            }
            else if (ppuScanline == 261 && ppuDot == 2)
            {
                ppuStatus &= 0x1F;
                NMIRequested = false;
            }

            if (ppuScanline < 240)
            {
                if (ppuDot == 1) EvaluateSpritesForScanline(ppuScanline);
                if (ppuDot is >= 1 and <= 256)
                {
                    int x = ppuDot - 1;
                    RenderPixel(x, ppuScanline);
                    if (ppuScanline == 239 && x == 255) PublishFrame();
                }
            }

            if (ppuDot >= 341)
            {
                ppuDot = 0;
                ppuScanline++;
                if (ppuScanline >= 262)
                {
                    ppuScanline = 0;
                    oddFrame = !oddFrame;
                }
            }
            else if (ppuScanline == 261 && ppuDot == 340 && oddFrame && (ppuMask & 0x18) != 0)
            {
                ppuDot = 0;
                ppuScanline = 0;
                oddFrame = !oddFrame;
            }
        }
    }

    private void RenderBackground()
    {
        for (int y = 0; y < 240; y++)
        {
            EvaluateSpritesForScanline(y);
            for (int x = 0; x < 256; x++)
                RenderPixel(x, y);
        }
        PublishFrame();
    }

    private void RenderPixel(int x, int y)
    {
        int frameIndex = y * 256 + x;
        byte backdrop = ReadPpuMemory(0x3F00);
        int bgColor = 0;
        int bgPalette = 0;
        if ((ppuMask & 0x08) != 0 && (x >= 8 || (ppuMask & 0x02) != 0))
        {
            int worldX = (x + scrollX) % 512;
            int worldY = (y + scrollY) % 480;
            int ntX = worldX / 256;
            int ntY = worldY / 240;
            int tileX = (worldX % 256) / 8;
            int tileY = (worldY % 240) / 8;
            int pixelX = worldX & 7;
            int pixelY = worldY & 7;
            int nametable = ((ppuControl & 3) + ntY * 2 + ntX) & 3;
            byte tile = ReadPpuMemory((ushort)(0x2000 + nametable * 0x400 + tileY * 32 + tileX));
            int patternBase = (ppuControl & 0x10) != 0 ? 0x1000 : 0;
            int pattern = patternBase + tile * 16 + pixelY;
            bgColor = ((ReadPpuMemory((ushort)pattern) >> (7 - pixelX)) & 1)
                | (((ReadPpuMemory((ushort)(pattern + 8)) >> (7 - pixelX)) & 1) << 1);
            if (bgColor != 0)
            {
                byte attribute = ReadPpuMemory((ushort)(0x23C0 + nametable * 0x400 + (tileY / 4) * 8 + tileX / 4));
                int shift = ((tileY & 2) << 1) | (tileX & 2);
                bgPalette = (attribute >> shift) & 3;
            }
        }

        backgroundPixels[frameIndex] = (byte)bgColor;
        frameBuffer[frameIndex] = (ppuMask & 0x08) == 0
            ? 0
            : GetNesColor(ReadPpuMemory((ushort)(0x3F00 + bgPalette * 4 + bgColor)));
        if ((ppuMask & 0x10) == 0 || (x < 8 && (ppuMask & 0x04) == 0)) return;

        int spriteHeight = (ppuControl & 0x20) != 0 ? 16 : 8;
        for (int selected = 0; selected < scanlineSpriteCount; selected++)
        {
            int sprite = scanlineSprites[selected];
            int oamIndex = sprite * 4;
            int top = oam[oamIndex] == 0xFF ? 0 : oam[oamIndex] + 1;
            int row = y - top;
            int column = x - oam[oamIndex + 3];
            if ((uint)row >= spriteHeight || (uint)column >= 8) continue;

            byte tile = oam[oamIndex + 1];
            byte attributes = oam[oamIndex + 2];
            if ((attributes & 0x80) != 0) row = spriteHeight - 1 - row;
            if ((attributes & 0x40) != 0) column = 7 - column;
            int patternBase;
            int tileNumber;
            if (spriteHeight == 16)
            {
                patternBase = (tile & 1) * 0x1000;
                tileNumber = (tile & 0xFE) + (row >= 8 ? 1 : 0);
            }
            else
            {
                patternBase = (ppuControl & 0x08) != 0 ? 0x1000 : 0;
                tileNumber = tile;
            }
            int pattern = patternBase + tileNumber * 16 + (row & 7);
            int bit = 7 - column;
            int color = ((ReadPpuMemory((ushort)pattern) >> bit) & 1)
                | (((ReadPpuMemory((ushort)(pattern + 8)) >> bit) & 1) << 1);
            if (color == 0) continue;

            if (sprite == 0 && bgColor != 0 && x < 255 && (ppuMask & 0x18) == 0x18)
                ppuStatus |= 0x40;

            if ((attributes & 0x20) == 0 || bgColor == 0)
                frameBuffer[frameIndex] = GetNesColor(ReadPpuMemory((ushort)(0x3F10 + (attributes & 3) * 4 + color)));
            break;
        }
    }

    private void EvaluateSpritesForScanline(int y)
    {
        scanlineSpriteCount = 0;
        int spriteHeight = (ppuControl & 0x20) != 0 ? 16 : 8;
        int spriteIndex = 0;
        for (int sprite = 0; sprite < 64; sprite++)
        {
            int row = SpriteRowForScanline(oam[sprite * 4], y);
            if ((uint)row >= spriteHeight) continue;
            if (scanlineSpriteCount < scanlineSprites.Length)
            {
                scanlineSprites[scanlineSpriteCount++] = sprite;
                spriteIndex = sprite + 1;
                if (scanlineSpriteCount == scanlineSprites.Length) break;
            }
        }

        if (scanlineSpriteCount < 8) return;

        // After secondary OAM fills, the PPU's broken n/m counters walk diagonally
        // through OAM bytes. Non-Y bytes can therefore be mistaken for Y positions.
        int byteIndex = 0;
        while (spriteIndex < 64)
        {
            byte value = oam[spriteIndex * 4 + byteIndex];
            int row = SpriteRowForScanline(value, y);
            if ((uint)row < spriteHeight)
            {
                ppuStatus |= 0x20;
                return;
            }

            spriteIndex++;
            byteIndex = (byte)((byteIndex + 1) & 3);
        }
    }

    private static int SpriteRowForScanline(byte spriteY, int scanline) =>
        scanline - (spriteY == 0xFF ? 0 : spriteY + 1);

    private static uint GetNesColor(byte index)
    {
        return NesPalette[index & 0x3F];
    }

    private uint[] CopyFrameBuffer() => (uint[])frameBuffer.Clone();

    private void PublishFrame() => FrameReady?.Invoke();

    public void EmulatePPU() => RenderBackground();
}
