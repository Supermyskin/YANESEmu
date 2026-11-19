using System;
using System.IO;

internal static class Program
{
    private static void Main(string[] args)
    {
        bool trace = Array.Exists(args, arg => arg == "--trace");
        string? romPath = Array.Find(args, arg => arg != "--trace");
        string path = romPath ?? Path.Combine(Directory.GetCurrentDirectory(), "7_Graphics.nes");
        Emulator emulator = new();
        emulator.Diagnostic += Console.WriteLine;
        if (trace) emulator.InstructionExecuted += CpuTraceLogger.Write;
        TerminalFramePresenter presenter = new();
        emulator.FrameReady += () => presenter.Display(emulator.GetFrameBuffer());

        if (!emulator.Reset(path))
        {
            Console.WriteLine("Usage: dotnet run -- <rom-file.nes> [--trace]");
            return;
        }

        Console.WriteLine($"Running {path}");
        emulator.Run();
        uint[] frame = emulator.GetFrameBuffer();
        presenter.Display(frame, true);
        string framePath = Path.Combine(Directory.GetCurrentDirectory(), "frame.bmp");
        presenter.SaveBmp(frame, framePath);
        Console.WriteLine($"Frame saved to {framePath}");
        Console.WriteLine($"Emulator halted. Final PC: 0x{emulator.ProgramCounter:X4}");
        Console.WriteLine($"A : 0x{emulator.A:X2} ({emulator.A})");
        Console.WriteLine($"X : 0x{emulator.X:X2} ({emulator.X})");
        Console.WriteLine($"Y : 0x{emulator.Y:X2} ({emulator.Y})");
        Console.WriteLine($"RAM[0x00] = 0x{emulator.RAM[0]:X2}");
        Console.WriteLine($"RAM[0x01] = 0x{emulator.RAM[1]:X2}");
        Console.WriteLine($"RAM[0x02] = 0x{emulator.RAM[2]:X2}");
    }
}
