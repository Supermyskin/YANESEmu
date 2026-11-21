using System;
using System.Threading;

public sealed class AudioSampleBuffer
{
    private readonly float[] samples = new float[32768];
    private int readIndex;
    private int writeIndex;

    public void Write(float sample)
    {
        int next = (writeIndex + 1) % samples.Length;
        if (next == Volatile.Read(ref readIndex)) return;
        samples[writeIndex] = sample;
        Volatile.Write(ref writeIndex, next);
    }

    public int Read(Span<float> destination)
    {
        int read = readIndex;
        int write = Volatile.Read(ref writeIndex);
        int count = 0;
        while (count < destination.Length && read != write)
        {
            destination[count++] = samples[read];
            read = (read + 1) % samples.Length;
        }
        Volatile.Write(ref readIndex, read);
        destination[count..].Clear();
        return count;
    }

    public float ReadSample()
    {
        int read = readIndex;
        if (read == Volatile.Read(ref writeIndex)) return 0;
        float sample = samples[read];
        Volatile.Write(ref readIndex, (read + 1) % samples.Length);
        return sample;
    }

    public void Clear()
    {
        Volatile.Write(ref readIndex, Volatile.Read(ref writeIndex));
    }
}
