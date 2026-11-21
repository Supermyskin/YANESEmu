using System;

internal sealed class NesApu
{
    private static readonly byte[] LengthTable =
    {
        10,254,20,2,40,4,80,6,160,8,60,10,14,12,26,14,
        12,16,24,18,48,20,96,22,192,24,72,26,16,28,32,30
    };

    private static readonly ushort[] NoisePeriods =
        { 4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068 };
    private static readonly byte[] DutyPatterns = { 0b00000001, 0b00000011, 0b00001111, 0b11111100 };
    private static readonly byte[] TriangleWave =
    {
        15,14,13,12,11,10,9,8,7,6,5,4,3,2,1,0,
        0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15
    };

    private readonly byte[,] pulseRegisters = new byte[2, 4];
    private readonly byte[] triangleRegisters = new byte[4];
    private readonly byte[] noiseRegisters = new byte[4];
    private readonly byte[] lengthCounters = new byte[4];
    private readonly byte[] envelopeDecay = new byte[3];
    private readonly bool[] envelopeStart = new bool[3];
    private readonly byte[] sweepDivider = new byte[2];
    private readonly bool[] sweepReload = new bool[2];
    private readonly AudioSampleBuffer output = new();
    private int frameCounter;
    private int sampleCounter;
    private int pulseTimer1;
    private int pulseTimer2;
    private int triangleTimer;
    private int noiseTimer;
    private int pulseSequence1;
    private int pulseSequence2;
    private int triangleSequence;
    private int noiseShift = 1;
    private int triangleLinearCounter;
    private bool triangleReload;
    private bool pulse1Enabled;
    private bool pulse2Enabled;
    private bool triangleEnabled;
    private bool noiseEnabled;
    private bool fiveStepMode;
    private int cpuParity;
    private float previousMixInput;
    private float previousMixOutput;

    public AudioSampleBuffer Output => output;

    public void Reset()
    {
        Array.Clear(pulseRegisters, 0, pulseRegisters.Length);
        Array.Clear(triangleRegisters, 0, triangleRegisters.Length);
        Array.Clear(noiseRegisters, 0, noiseRegisters.Length);
        Array.Clear(lengthCounters, 0, lengthCounters.Length);
        Array.Clear(envelopeDecay, 0, envelopeDecay.Length);
        Array.Clear(envelopeStart, 0, envelopeStart.Length);
        Array.Clear(sweepDivider, 0, sweepDivider.Length);
        Array.Clear(sweepReload, 0, sweepReload.Length);
        frameCounter = sampleCounter = pulseTimer1 = pulseTimer2 = triangleTimer = noiseTimer = 0;
        pulseSequence1 = pulseSequence2 = triangleSequence = 0;
        noiseShift = 1;
        triangleLinearCounter = 0;
        triangleReload = false;
        pulse1Enabled = pulse2Enabled = triangleEnabled = noiseEnabled = fiveStepMode = false;
        cpuParity = 0;
        previousMixInput = previousMixOutput = 0;
        output.Clear();
    }

    public void WriteRegister(ushort address, byte value)
    {
        if (address is >= 0x4000 and <= 0x4007)
        {
            int channel = (address - 0x4000) / 4;
            int register = (address - 0x4000) & 3;
            pulseRegisters[channel, register] = value;
            if (register == 1) sweepReload[channel] = true;
            if (register == 3)
            {
                if ((channel == 0 ? pulse1Enabled : pulse2Enabled))
                    lengthCounters[channel] = LengthTable[value >> 3];
                if (channel == 0) { pulseSequence1 = 0; envelopeStart[0] = true; }
                else { pulseSequence2 = 0; envelopeStart[1] = true; }
            }
            return;
        }

        switch (address)
        {
            case 0x4008: case 0x4009: case 0x400A: case 0x400B:
                triangleRegisters[address - 0x4008] = value;
                if (address == 0x400B)
                {
                    if (triangleEnabled) lengthCounters[2] = LengthTable[value >> 3];
                    triangleReload = true;
                }
                break;
            case 0x400C: case 0x400D: case 0x400E: case 0x400F:
                noiseRegisters[address - 0x400C] = value;
                if (address == 0x400F)
                {
                    if (noiseEnabled) lengthCounters[3] = LengthTable[value >> 3];
                    envelopeStart[2] = true;
                }
                break;
            case 0x4015:
                pulse1Enabled = (value & 1) != 0;
                pulse2Enabled = (value & 2) != 0;
                triangleEnabled = (value & 4) != 0;
                noiseEnabled = (value & 8) != 0;
                for (int channel = 0; channel < 4; channel++)
                    if ((value & (1 << channel)) == 0) lengthCounters[channel] = 0;
                break;
            case 0x4017:
                fiveStepMode = (value & 0x80) != 0;
                frameCounter = 0;
                if (fiveStepMode) { QuarterFrame(); HalfFrame(); }
                break;
        }
    }

    public byte ReadStatus()
    {
        return (byte)((lengthCounters[0] > 0 ? 1 : 0) |
                      (lengthCounters[1] > 0 ? 2 : 0) |
                      (lengthCounters[2] > 0 ? 4 : 0) |
                      (lengthCounters[3] > 0 ? 8 : 0));
    }

    public void Clock(int cpuCycles)
    {
        for (int i = 0; i < cpuCycles; i++)
        {
            cpuParity ^= 1;
            if (cpuParity == 0)
            {
                ClockPulseTimers();
                ClockNoiseTimer();
            }
            ClockTriangleTimer();
            ClockFrameCounter();

            sampleCounter += 48000;
            if (sampleCounter >= 1789773)
            {
                sampleCounter -= 1789773;
                output.Write(MixChannels());
            }
        }
    }

    private void ClockPulseTimers()
    {
        if (pulseTimer1-- <= 0) { pulseTimer1 = PulseTimer(0); pulseSequence1 = (pulseSequence1 + 1) & 7; }
        if (pulseTimer2-- <= 0) { pulseTimer2 = PulseTimer(1); pulseSequence2 = (pulseSequence2 + 1) & 7; }
    }

    private int PulseTimer(int channel) => pulseRegisters[channel, 2] | ((pulseRegisters[channel, 3] & 7) << 8);

    private void ClockTriangleTimer()
    {
        int timerPeriod = triangleRegisters[2] | ((triangleRegisters[3] & 7) << 8);
        if (triangleTimer-- <= 0)
        {
            triangleTimer = timerPeriod;
            if (triangleEnabled && lengthCounters[2] > 0 && triangleLinearCounter > 0 && timerPeriod > 1)
                triangleSequence = (triangleSequence + 1) & 31;
        }
    }

    private void ClockNoiseTimer()
    {
        int period = NoisePeriods[noiseRegisters[2] & 0x0F];
        if (noiseTimer-- <= 0)
        {
            noiseTimer = period;
            int tap = (noiseRegisters[2] & 0x80) != 0 ? 6 : 1;
            int feedback = (noiseShift & 1) ^ ((noiseShift >> tap) & 1);
            noiseShift = (noiseShift >> 1) | (feedback << 14);
        }
    }

    private void ClockFrameCounter()
    {
        frameCounter++;
        if (frameCounter is 7457 or 22371) QuarterFrame();
        if (frameCounter is 14913 or 29829) { QuarterFrame(); HalfFrame(); }
        int end = fiveStepMode ? 37281 : 29830;
        if (frameCounter >= end)
        {
            frameCounter = 0;
            if (fiveStepMode) { QuarterFrame(); HalfFrame(); }
        }
    }

    private void QuarterFrame()
    {
        for (int channel = 0; channel < 3; channel++)
        {
            if (envelopeStart[channel])
            {
                envelopeStart[channel] = false;
                envelopeDecay[channel] = 15;
            }
            else if (envelopeDecay[channel] > 0)
            {
                envelopeDecay[channel]--;
            }
            else if ((EnvelopeRegister(channel) & 0x20) != 0)
            {
                envelopeDecay[channel] = 15;
            }
        }

        if (triangleReload) triangleLinearCounter = triangleRegisters[0] & 0x7F;
        else if (triangleLinearCounter > 0) triangleLinearCounter--;
        if ((triangleRegisters[0] & 0x80) == 0) triangleReload = false;
    }

    private byte EnvelopeRegister(int channel) => channel switch
    {
        0 => pulseRegisters[0, 0],
        1 => pulseRegisters[1, 0],
        _ => noiseRegisters[0]
    };

    private void HalfFrame()
    {
        ClockSweep(0);
        ClockSweep(1);
        for (int channel = 0; channel < 4; channel++)
        {
            bool halt = channel switch
            {
                0 => (pulseRegisters[0, 0] & 0x20) != 0,
                1 => (pulseRegisters[1, 0] & 0x20) != 0,
                2 => (triangleRegisters[0] & 0x80) != 0,
                _ => (noiseRegisters[0] & 0x20) != 0
            };
            if (!halt && lengthCounters[channel] > 0) lengthCounters[channel]--;
        }
    }

    private void ClockSweep(int channel)
    {
        byte sweep = pulseRegisters[channel, 1];
        int dividerPeriod = (sweep >> 4) & 7;
        int timerPeriod = PulseTimer(channel);
        int shift = sweep & 7;
        int change = timerPeriod >> shift;
        int target = (sweep & 0x08) != 0
            ? timerPeriod - change - (channel == 0 ? 1 : 0)
            : timerPeriod + change;

        if (sweepDivider[channel] == 0 && (sweep & 0x80) != 0 && shift != 0
            && timerPeriod >= 8 && target is >= 0 and <= 0x7FF)
        {
            pulseRegisters[channel, 2] = (byte)target;
            pulseRegisters[channel, 3] = (byte)((pulseRegisters[channel, 3] & 0xF8) | (target >> 8));
        }

        if (sweepDivider[channel] == 0 || sweepReload[channel])
        {
            sweepDivider[channel] = (byte)dividerPeriod;
            sweepReload[channel] = false;
        }
        else
        {
            sweepDivider[channel]--;
        }
    }

    private float MixChannels()
    {
        int pulse1 = PulseOutput(0, pulseSequence1, pulse1Enabled);
        int pulse2 = PulseOutput(1, pulseSequence2, pulse2Enabled);
        int triangle = triangleEnabled && lengthCounters[2] > 0 && triangleLinearCounter > 0
            ? TriangleWave[triangleSequence] : 0;
        int noise = noiseEnabled && lengthCounters[3] > 0 && (noiseShift & 1) == 0
            ? EnvelopeVolume(2) : 0;

        double pulseOut = pulse1 + pulse2 == 0 ? 0 : 95.88 / (8128.0 / (pulse1 + pulse2) + 100);
        double tndInput = triangle / 8227.0 + noise / 12241.0;
        double tndOut = tndInput == 0 ? 0 : 159.79 / (1.0 / tndInput + 100);
        float input = (float)((pulseOut + tndOut) * 0.8);
        float output = input - previousMixInput + previousMixOutput * 0.995f;
        previousMixInput = input;
        previousMixOutput = output;
        return Math.Clamp(output, -1f, 1f);
    }

    private int PulseOutput(int channel, int sequence, bool enabled)
    {
        if (!enabled || lengthCounters[channel] == 0 || PulseTimer(channel) < 8) return 0;
        int duty = pulseRegisters[channel, 0] >> 6;
        if (((DutyPatterns[duty] >> sequence) & 1) == 0) return 0;
        return EnvelopeVolume(channel);
    }

    private int EnvelopeVolume(int channel)
    {
        byte register = EnvelopeRegister(channel);
        return (register & 0x10) != 0 ? register & 0x0F : envelopeDecay[channel];
    }
}
