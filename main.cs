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

public void Reset() {
    byte[] HeaderedROM = File.ReadAllBytes(filepath);
    Array.Copy(HeaderedROM, 0x10, ROM, 0, 0x8000);
    Array.Copy(HeaderedROM, Header, 0x10);
    byte ProgramCounterLow = Read(0xFFFC); // low byte
    byte ProgramCounterHigh = Read(0xFFFD); // high byte
    ProgramCounter = (ushort)((ProgramCounterHigh * 0x100) + ProgramCounterLow);
}

private void button1_Click(object sender, EventArgs e) {
    Emulator emulator = new Emulator();
    emulator.filepath = @"/Users/damian/Programming/YANESEmu/1_Example.nes";
    emulator.Reset();
}