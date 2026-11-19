using System;
using System.IO;

public partial class Emulator
{
    public byte[] RAM = new byte[0x800];
    public byte[] ROM = new byte[0x8000];
    public byte[] CHRData = new byte[0x2000];
    public byte[] CHRROM = new byte[0x2000];

    public bool Reset(string filepath)
    {
        if (!File.Exists(filepath))
        {
            Diagnostic?.Invoke($"Error: File not found at {filepath}");
            return false;
        }

        byte[] fileBytes = File.ReadAllBytes(filepath);

        if (fileBytes.Length < 16 ||
            fileBytes[0] != 0x4E ||
            fileBytes[1] != 0x45 ||
            fileBytes[2] != 0x53 ||
            fileBytes[3] != 0x1A)
        {
            Diagnostic?.Invoke("Error: Invalid iNES ROM file");
            return false;
        }

        int mapper = (fileBytes[6] >> 4) | (fileBytes[7] & 0xF0);
        if (mapper != 0 || (fileBytes[7] & 0x0C) == 0x08 ||
            fileBytes[4] is not (1 or 2) || fileBytes[5] > 1)
        {
            Diagnostic?.Invoke("Error: Only iNES NROM ROMs with 1–2 PRG banks and up to 1 CHR bank are supported");
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
            Diagnostic?.Invoke("Error: ROM file is incomplete");
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
                Diagnostic?.Invoke("Error: ROM file is incomplete (CHR data is truncated)");
                return false;
            }

            int bytesToLoad = Math.Min(chrSize, CHRData.Length);
            Array.Copy(fileBytes, chrOffset, CHRData, 0, bytesToLoad);
            Array.Copy(CHRData, CHRROM, CHRData.Length);
        }

        return true;
    }

}
