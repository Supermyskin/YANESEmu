ushort ProgramCounter;
byte A;
byte X;
byte Y;

byte[] RAM = new byte[0x800];
byte[] ROM = new byte[0x8000];

byte Read(ushort Addr) {
    if (Addr < 0x800) {
        return RAM[Addr]
    }
    if (Addr >= 0x8000) {
        return ROM[Addr-0x8000]
    }
}