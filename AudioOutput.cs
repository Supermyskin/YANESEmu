using System;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Metadata.Models;
using SoundFlow.Structs;

internal sealed class EmulatorAudioProvider : ISoundDataProvider
{
    private readonly AudioSampleBuffer samples;
    private int position;
    private int disposed;
    private float volume = 0.75f;
    private int muted;

    public EmulatorAudioProvider(AudioSampleBuffer samples) => this.samples = samples;
    public int Position => position;
    public int Length => -1;
    public bool CanSeek => false;
    public SampleFormat SampleFormat => SampleFormat.F32;
    public int SampleRate => 48000;
    public bool IsDisposed => Volatile.Read(ref disposed) != 0;
    public SoundFormatInfo? FormatInfo => null;
    public float Volume { get => Volatile.Read(ref volume); set => Volatile.Write(ref volume, Math.Clamp(value, 0, 1)); }
    public bool Muted { get => Volatile.Read(ref muted) != 0; set => Volatile.Write(ref muted, value ? 1 : 0); }

    public event EventHandler<EventArgs>? EndOfStreamReached { add { } remove { } }
    public event EventHandler<PositionChangedEventArgs>? PositionChanged { add { } remove { } }

    public int ReadBytes(Span<float> buffer)
    {
        if (IsDisposed) return 0;
        float gain = Muted ? 0 : Volume;
        for (int i = 0; i < buffer.Length; i += 2)
        {
            float sample = samples.ReadSample() * gain;
            buffer[i] = sample;
            if (i + 1 < buffer.Length) buffer[i + 1] = sample;
        }
        position += buffer.Length;
        return buffer.Length;
    }

    public void Seek(int offset) => throw new NotSupportedException("Live emulator audio cannot be seeked.");
    public void Dispose() => Interlocked.Exchange(ref disposed, 1);
}

internal sealed class AudioOutput : IDisposable
{
    private readonly AudioEngine engine;
    private readonly AudioPlaybackDevice device;
    private readonly SoundPlayer player;
    private readonly EmulatorAudioProvider provider;
    private int disposed;

    public AudioOutput(AudioSampleBuffer samples, float volume, bool muted)
    {
        engine = new MiniAudioEngine();
        AudioFormat format = new()
        {
            Format = SampleFormat.F32,
            Channels = 2,
            Layout = ChannelLayout.Stereo,
            SampleRate = 48000
        };
        provider = new EmulatorAudioProvider(samples) { Volume = volume, Muted = muted };
        player = new SoundPlayer(engine, format, provider);
        device = engine.InitializePlaybackDevice(null, format);
        device.MasterMixer.AddComponent(player);
        device.Start();
        player.Play();
    }

    public float Volume { get => provider.Volume; set => provider.Volume = value; }
    public bool Muted { get => provider.Muted; set => provider.Muted = value; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        player.Stop();
        device.Stop();
        device.MasterMixer.RemoveComponent(player);
        player.Dispose();
        provider.Dispose();
        device.Dispose();
        engine.Dispose();
    }
}
