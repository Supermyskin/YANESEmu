using System;
using System.Diagnostics;
using System.IO;
using System.Text;

internal sealed class TerminalFramePresenter
{
    private long lastDisplayTicks;
    private bool frameStarted;

    public void Display(uint[] frame, bool force = false)
    {
        if (Console.IsOutputRedirected) return;
        long now = Stopwatch.GetTimestamp();
        if (!force && lastDisplayTicks != 0 && Stopwatch.GetElapsedTime(lastDisplayTicks, now).TotalMilliseconds < 66) return;
        lastDisplayTicks = now;
        StringBuilder output = new(64 * 120 * 48);
        output.Append(frameStarted ? "\x1b[H" : "\x1b[2J\x1b[H");
        frameStarted = true;
        for (int y = 0; y < 240; y += 2)
        {
            for (int x = 0; x < 256; x += 4)
            {
                uint top = Average(frame, y, x);
                uint bottom = Average(frame, y + 1, x);
                output.Append("\x1b[38;2;").Append((top >> 16) & 255).Append(';').Append((top >> 8) & 255).Append(';').Append(top & 255);
                output.Append("m\x1b[48;2;").Append((bottom >> 16) & 255).Append(';').Append((bottom >> 8) & 255).Append(';').Append(bottom & 255).Append("m▀");
            }
            output.Append("\x1b[0m\n");
        }
        Console.Write(output.ToString());
    }

    public void SaveBmp(uint[] frame, string path)
    {
        const int width = 256, height = 240, rowSize = width * 3;
        int paddedRowSize = (rowSize + 3) & ~3;
        int imageSize = paddedRowSize * height;
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream);
        writer.Write((byte)'B'); writer.Write((byte)'M'); writer.Write(54 + imageSize);
        writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(width); writer.Write(height);
        writer.Write((short)1); writer.Write((short)24); writer.Write(0); writer.Write(imageSize);
        writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                uint color = frame[y * width + x];
                writer.Write((byte)color); writer.Write((byte)(color >> 8)); writer.Write((byte)(color >> 16));
            }
            for (int pad = rowSize; pad < paddedRowSize; pad++) writer.Write((byte)0);
        }
    }

    private static uint Average(uint[] frame, int y, int x)
    {
        uint red = 0, green = 0, blue = 0;
        for (int i = 0; i < 4; i++)
        {
            uint color = frame[y * 256 + x + i];
            red += (color >> 16) & 255; green += (color >> 8) & 255; blue += color & 255;
        }
        return ((red / 4) << 16) | ((green / 4) << 8) | (blue / 4);
    }
}
