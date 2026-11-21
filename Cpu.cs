using System;

public partial class Emulator
{
    private static readonly string[] OpCodeNames =
	{
	"BRK","ORA","KIL","SLO","NOP","ORA","ASL","SLO","PHP","ORA","ASL","ANC","NOP","ORA","ASL","SLO",
	"BPL","ORA","KIL","SLO","NOP","ORA","ASL","SLO","CLC","ORA","NOP","SLO","NOP","ORA","ASL","SLO",
	"JSR","AND","KIL","RLA","BIT","AND","ROL","RLA","PLP","AND","ROL","ANC","BIT","AND","ROL","RLA",
	"BMI","AND","KIL","RLA","NOP","AND","ROL","RLA","SEC","AND","NOP","RLA","NOP","AND","ROL","RLA",
	"RTI","EOR","KIL","SRE","NOP","EOR","LSR","SRE","PHA","EOR","LSR","ALR","JMP","EOR","LSR","SRE",
	"BVC","EOR","KIL","SRE","NOP","EOR","LSR","SRE","CLI","EOR","NOP","SRE","NOP","EOR","LSR","SRE",
	"RTS","ADC","KIL","RRA","NOP","ADC","ROR","RRA","PLA","ADC","ROR","ARR","JMP","ADC","ROR","RRA",
	"BVS","ADC","KIL","RRA","NOP","ADC","ROR","RRA","SEI","ADC","NOP","RRA","NOP","ADC","ROR","RRA",
	"NOP","STA","NOP","SAX","STY","STA","STX","SAX","DEY","NOP","TXA","NOP","STY","STA","STX","SAX",
	"BCC","STA","KIL","SHA","STY","STA","STX","SAX","TYA","STA","TXS","SHS","SHY","STA","SHX","SHA",
	"LDY","LDA","LDX","LAX","LDY","LDA","LDX","LAX","TAY","LDA","TAX","LXA","LDY","LDA","LDX","LAX",
	"BCS","LDA","KIL","LAX","LDY","LDA","LDX","LAX","CLV","LDA","TSX","LAE","LDY","LDA","LDX","LAX",
	"CPY","CMP","NOP","DCP","CPY","CMP","DEC","DCP","INY","CMP","DEX","AXS","CPY","CMP","DEC","DCP",
	"BNE","CMP","KIL","DCP","NOP","CMP","DEC","DCP","CLD","CMP","NOP","DCP","NOP","CMP","DEC","DCP",
	"CPX","SBC","NOP","ISC","CPX","SBC","INC","ISC","INX","SBC","NOP","SBC","CPX","SBC","INC","ISC",
	"BEQ","SBC","KIL","ISC","NOP","SBC","INC","ISC","SED","SBC","NOP","ISC","NOP","SBC","INC","ISC",
    };

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

    private int ServiceInterrupt(ushort vector, bool isNmi)
    {
        Push((byte)(ProgramCounter >> 8));
        Push((byte)ProgramCounter);
        byte status = (byte)((flag_Carry ? 1 : 0) | (flag_Zero ? 2 : 0) |
            (flag_InterruptDisable ? 4 : 0) | (flag_Decimal ? 8 : 0) | 0x20 |
            (flag_Overflow ? 0x40 : 0) | (flag_Negative ? 0x80 : 0));
        Push(status); // B is clear for hardware interrupts.
        flag_InterruptDisable = true;
        ProgramCounter = ReadWord(vector);
        if (isNmi) NMIRequested = false;
        return FinishStep(7);
    }

    public int Step()
    {
        if (CPU_Halted) return FinishStep(0);

        if (NMIRequested)
            return ServiceInterrupt(0xFFFA, isNmi: true);
        if (IRQRequested && !flag_InterruptDisable)
            return ServiceInterrupt(0xFFFE, isNmi: false);

        ushort pcBeforeStep = ProgramCounter;
        opcode = Read(ProgramCounter++);
        ushort targetAddr;
        byte Temp_High;
        byte Temp_Low;
        byte Temp;

        InstructionExecuted?.Invoke(this, pcBeforeStep, opcode);

        switch (opcode)
        {
            case 0x02: case 0x12: case 0x22: case 0x32: case 0x42: case 0x52:
            case 0x62: case 0x72: case 0x92: case 0xB2: case 0xD2: case 0xF2: // unofficial KIL
                CPU_Halted = true;
                Diagnostic?.Invoke($"Unofficial KIL opcode 0x{opcode:X2} halted the CPU at PC: 0x{(ProgramCounter - 1):X4}");
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


            case 0x1A: case 0x3A: case 0x5A: case 0x7A: case 0xDA: case 0xFA: // unofficial implied NOP
                return FinishStep(2);

            case 0x80: case 0x82: case 0x89: case 0xC2: case 0xE2: // unofficial immediate NOP
                ProgramCounter++;
                return FinishStep(2);

            case 0x04: case 0x44: case 0x64: // unofficial zero-page NOP
            {
                ushort zpAddress = Read(ProgramCounter++);
                Read(zpAddress);
                return FinishStep(3);
            }

            case 0x14: case 0x34: case 0x54: case 0x74: case 0xD4: case 0xF4: // unofficial zero-page,X NOP
                ReadOperands_ZeroPageXAddressed();
                Read(addressBus);
                return FinishStep(4);

            case 0x0C: // unofficial absolute NOP
                ReadOperands_AbsoluteAdressed();
                Read(addressBus);
                return FinishStep(4);

            case 0x1C: case 0x3C: case 0x5C: case 0x7C: case 0xDC: case 0xFC: // unofficial absolute,X NOP
                ReadOperands_AbsoluteXAddressed();
                Read(addressBus);
                return FinishStep(4 + (pageCrossed ? 1 : 0));

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
                return FinishStep(5 + (pageCrossed ? 1 : 0));

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
                Diagnostic?.Invoke($"Unsupported opcode {GetOpcodeName(opcode)} (0x{opcode:X2}) at PC: 0x{(ProgramCounter - 1):X4}");
                CPU_Halted = true;
                return FinishStep(2);
        }

    }

    public static string GetOpcodeName(byte opcode) => OpCodeNames[opcode];
}
