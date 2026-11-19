using System;

internal static class CpuTraceLogger
{
    public static void Write(Emulator emulator, ushort pc, byte opcode)
    {
        byte status = 0x20;
        if (emulator.flag_Carry) status |= 0x01;
        if (emulator.flag_Zero) status |= 0x02;
        if (emulator.flag_InterruptDisable) status |= 0x04;
        if (emulator.flag_Decimal) status |= 0x08;
        if (emulator.flag_Overflow) status |= 0x40;
        if (emulator.flag_Negative) status |= 0x80;
        string flags = $"{(emulator.flag_Negative ? 'N' : 'n')}{(emulator.flag_Overflow ? 'V' : 'v')}--" +
            $"{(emulator.flag_Decimal ? 'D' : 'd')}{(emulator.flag_InterruptDisable ? 'I' : 'i')}" +
            $"{(emulator.flag_Zero ? 'Z' : 'z')}{(emulator.flag_Carry ? 'C' : 'c')}";
        byte b1 = emulator.Read((ushort)(pc + 1));
        byte b2 = emulator.Read((ushort)(pc + 2));
        Console.WriteLine($"{pc:X4}  {opcode:X2} {b1:X2} {b2:X2}  {Emulator.GetOpcodeName(opcode),-4} " +
            $"\tA:{emulator.A:X2} X:{emulator.X:X2} Y:{emulator.Y:X2} P:{status:X2} {flags} " +
            $"SP:{emulator.stackPointer:X2} CYC:{emulator.TotalCycles,3}");
    }
}
