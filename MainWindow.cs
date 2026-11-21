using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

internal sealed class MainWindow : Window
{
    private const int FrameWidth = 256;
    private const int FrameHeight = 240;
    private readonly Emulator emulator = new();
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private readonly WriteableBitmap bitmap = new(
        new PixelSize(FrameWidth, FrameHeight), new Vector(96, 96),
        PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private readonly Image frameImage;
    private readonly TextBlock statusText;
    private readonly DispatcherTimer frameTimer;
    private readonly byte[] bgraFrame = new byte[FrameWidth * FrameHeight * 4];
    private string? currentRomPath;
    private NativeMenuItem? nativePauseItem;
    private MenuItem? windowPauseItem;
    private Task? runTask;
    private TaskCompletionSource pauseAcknowledged = NewCompletionSource();
    private TaskCompletionSource resumeSignal = NewCompletionSource();
    private volatile bool isRunning;
    private volatile bool isPaused;
    private volatile bool hasDiagnostic;
    private bool closePending;
    private bool allowClose;
    private bool shutdownRequested;

    public MainWindow()
    {
        Title = "YANESEmu";
        Width = 900;
        Height = 700;
        MinWidth = 520;
        MinHeight = 420;
        Background = new SolidColorBrush(Color.Parse("#171717"));

        frameImage = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        RenderOptions.SetBitmapInterpolationMode(frameImage, BitmapInterpolationMode.None);

        statusText = new TextBlock
        {
            Text = "Open a NES ROM to begin.",
            Margin = new Thickness(12, 8),
            Foreground = Brushes.Gainsboro,
            TextWrapping = TextWrapping.Wrap
        };

        Menu? menu = null;
        if (OperatingSystem.IsMacOS())
            NativeMenu.SetMenu(this, BuildNativeMenu());
        else
            menu = BuildWindowMenu();
        Border displayBorder = new()
        {
            Child = frameImage,
            Background = Brushes.Black,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            ClipToBounds = true
        };
        Grid.SetRow(displayBorder, 1);
        Grid.SetRow(statusText, 2);
        Grid layout = new();
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (menu is not null)
        {
            Grid.SetRow(menu, 0);
            layout.Children.Add(menu);
        }
        layout.Children.Add(displayBorder);
        layout.Children.Add(statusText);
        Content = layout;

        emulator.Diagnostic += message =>
        {
            hasDiagnostic = true;
            Dispatcher.UIThread.Post(() => SetStatus(message, isError: true));
        };
        if (YanesEmuApp.TraceEnabled)
            emulator.InstructionExecuted += CpuTraceLogger.Write;
        frameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        frameTimer.Tick += (_, _) => UpdateFrame();
        frameTimer.Start();
        Closing += HandleClosing;
        Closed += (_, _) =>
        {
            frameTimer.Stop();
            bitmap.Dispose();
        };
    }

    private Menu BuildWindowMenu()
    {
        MenuItem open = new() { Header = "Open ROM…" };
        open.Click += async (_, _) => await OpenRomAsync();
        MenuItem reset = new() { Header = "Reset", InputGesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.R, Avalonia.Input.KeyModifiers.Meta) };
        reset.Click += async (_, _) => await ResetRomAsync();
        windowPauseItem = new MenuItem { Header = "Pause" };
        windowPauseItem.Click += async (_, _) => await TogglePauseAsync();
        MenuItem step = new() { Header = "Step Instruction" };
        step.Click += async (_, _) => await StepInstructionAsync();
        MenuItem stop = new() { Header = "Stop" };
        stop.Click += async (_, _) => await StopFromMenuAsync();
        MenuItem quit = new() { Header = "Quit" };
        quit.Click += (_, _) => Close();
        MenuItem fileMenu = new() { Header = "File" };
        fileMenu.Items.Add(open);
        fileMenu.Items.Add(reset);
        fileMenu.Items.Add(windowPauseItem);
        fileMenu.Items.Add(step);
        fileMenu.Items.Add(stop);
        fileMenu.Items.Add(new Separator());
        fileMenu.Items.Add(quit);
        Menu menu = new();
        menu.Items.Add(fileMenu);
        return menu;
    }

    private NativeMenu BuildNativeMenu()
    {
        NativeMenu fileItems = new();
        NativeMenuItem open = new()
        {
            Header = "Open ROM…",
            Gesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.O, Avalonia.Input.KeyModifiers.Meta)
        };
        open.Click += async (_, _) => await OpenRomAsync();
        NativeMenuItem reset = new()
        {
            Header = "Reset",
            Gesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.R, Avalonia.Input.KeyModifiers.Meta)
        };
        reset.Click += async (_, _) => await ResetRomAsync();
        nativePauseItem = new NativeMenuItem { Header = "Pause" };
        nativePauseItem.Click += async (_, _) => await TogglePauseAsync();
        NativeMenuItem step = new() { Header = "Step Instruction" };
        step.Click += async (_, _) => await StepInstructionAsync();
        NativeMenuItem stop = new() { Header = "Stop" };
        stop.Click += async (_, _) => await StopFromMenuAsync();
        fileItems.Items.Add(open);
        fileItems.Items.Add(reset);
        fileItems.Items.Add(nativePauseItem);
        fileItems.Items.Add(step);
        fileItems.Items.Add(stop);
        NativeMenu menu = new();
        menu.Items.Add(new NativeMenuItem { Header = "File", Menu = fileItems });
        return menu;
    }

    private async Task OpenRomAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open NES ROM",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("NES ROMs") { Patterns = new[] { "*.nes" } },
                FilePickerFileTypes.All
            }
        });
        if (files.Count == 0) return;
        string? path = files[0].TryGetLocalPath();
        if (path is null)
        {
            SetStatus("Could not access the selected ROM file on this device.", isError: true);
            return;
        }
        if (shutdownRequested) return;
        await LoadRomAsync(path);
    }

    private async Task LoadRomAsync(string path)
    {
        await commandGate.WaitAsync();
        try
        {
            if (shutdownRequested) return;
            await StopEmulationAsync();
            if (shutdownRequested) return;
            currentRomPath = null;
            hasDiagnostic = false;
            try
            {
                if (!emulator.Reset(path))
                {
                    Title = "YANESEmu";
                    return;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                Title = "YANESEmu";
                SetStatus($"Could not open ROM: {ex.Message}", isError: true);
                return;
            }

            currentRomPath = path;
            Title = $"YANESEmu — {Path.GetFileName(path)}";
            isPaused = false;
            SetPauseMenuHeader("Pause");
            SetStatus($"Loaded {Path.GetFileName(path)}. Choose File → Pause to pause emulation.");
            StartEmulation();
        }
        finally
        {
            commandGate.Release();
        }
    }

    private async Task ResetRomAsync()
    {
        if (currentRomPath is not null) await LoadRomAsync(currentRomPath);
    }

    private async Task StopEmulationAsync()
    {
        isRunning = false;
        isPaused = false;
        resumeSignal.TrySetResult();
        pauseAcknowledged.TrySetResult();
        if (runTask is not null)
        {
            await runTask;
            runTask = null;
        }
    }

    private void StartEmulation()
    {
        isRunning = true;
        isPaused = false;
        pauseAcknowledged = NewCompletionSource();
        resumeSignal = NewCompletionSource();
        runTask = Task.Run(RunEmulationAsync);
    }

    private async Task RunEmulationAsync()
    {
        try
        {
            while (isRunning && !emulator.CPU_Halted)
            {
                if (isPaused)
                {
                    pauseAcknowledged.TrySetResult();
                    await resumeSignal.Task;
                    continue;
                }

                for (int i = 0; i < 2500 && isRunning && !isPaused && !emulator.CPU_Halted; i++)
                    emulator.Step();
                await Task.Yield();
            }
        }
        catch (Exception ex)
        {
            hasDiagnostic = true;
            Dispatcher.UIThread.Post(() => SetStatus($"Emulation error: {ex.Message}", isError: true));
        }
        finally
        {
            isRunning = false;
            pauseAcknowledged.TrySetResult();
        }

        if (emulator.CPU_Halted && !hasDiagnostic)
            Dispatcher.UIThread.Post(() =>
            {
                SetPauseMenuHeader("Pause");
                SetStatus($"CPU halted at 0x{emulator.ProgramCounter:X4}. Use Reset to start again.", isError: true);
            });
    }

    private async Task TogglePauseAsync()
    {
        await commandGate.WaitAsync();
        try
        {
            if (shutdownRequested || currentRomPath is null || !isRunning) return;
            if (isPaused)
            {
                TaskCompletionSource previousResume = resumeSignal;
                resumeSignal = NewCompletionSource();
                isPaused = false;
                pauseAcknowledged.TrySetResult();
                previousResume.TrySetResult();
                SetPauseMenuHeader("Pause");
                SetStatus($"Running {Path.GetFileName(currentRomPath)}.");
                return;
            }

            pauseAcknowledged = NewCompletionSource();
            isPaused = true;
            SetPauseMenuHeader("Resume");
            await pauseAcknowledged.Task;
            if (!isRunning || !isPaused) return;
            SetStatus("Paused.");
        }
        finally { commandGate.Release(); }
    }

    private async Task StepInstructionAsync()
    {
        await commandGate.WaitAsync();
        try
        {
            if (shutdownRequested || currentRomPath is null || emulator.CPU_Halted) return;
            if (isRunning && !isPaused)
            {
                SetStatus("Pause emulation before stepping one instruction.");
                return;
            }
            if (isRunning)
                await pauseAcknowledged.Task;
            else if (runTask is not null)
            {
                await runTask;
                runTask = null;
            }
            if (isRunning && !isPaused) return;

            await Task.Run(emulator.Step);
            UpdateFrame();
            if (emulator.CPU_Halted)
            {
                await StopEmulationAsync();
                SetPauseMenuHeader("Pause");
                if (!hasDiagnostic)
                    SetStatus($"CPU halted at 0x{emulator.ProgramCounter:X4}.", isError: true);
            }
            else
            {
                string state = isRunning ? "paused" : "stopped";
                SetStatus($"Stepped one instruction. PC: 0x{emulator.ProgramCounter:X4}. Emulation remains {state}.");
            }
        }
        finally { commandGate.Release(); }
    }

    private async Task StopFromMenuAsync()
    {
        await commandGate.WaitAsync();
        try
        {
            if (shutdownRequested || currentRomPath is null || !isRunning) return;
            await StopEmulationAsync();
            SetPauseMenuHeader("Pause");
            SetStatus($"Stopped by user at PC 0x{emulator.ProgramCounter:X4}. Use Reset to run again.");
        }
        finally { commandGate.Release(); }
    }

    private void SetPauseMenuHeader(string header)
    {
        if (nativePauseItem is not null) nativePauseItem.Header = header;
        if (windowPauseItem is not null) windowPauseItem.Header = header;
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async void HandleClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (closePending) return;
        closePending = true;
        shutdownRequested = true;
        await commandGate.WaitAsync();
        try { await StopEmulationAsync(); }
        finally { commandGate.Release(); }
        allowClose = true;
        Close();
    }

    private void UpdateFrame()
    {
        uint[] pixels = emulator.GetFrameBuffer();
        for (int i = 0; i < pixels.Length; i++)
        {
            uint color = pixels[i];
            int offset = i * 4;
            bgraFrame[offset] = (byte)color;
            bgraFrame[offset + 1] = (byte)(color >> 8);
            bgraFrame[offset + 2] = (byte)(color >> 16);
            bgraFrame[offset + 3] = 0xFF;
        }

        using ILockedFramebuffer locked = bitmap.Lock();
        int rowBytes = FrameWidth * 4;
        for (int y = 0; y < FrameHeight; y++)
            Marshal.Copy(bgraFrame, y * rowBytes, IntPtr.Add(locked.Address, y * locked.RowBytes), rowBytes);
        frameImage.InvalidateVisual();
    }

    public void ShowAbout() => SetStatus("YANESEmu — an early NES emulator written in C# and Avalonia.");

    private void SetStatus(string message, bool isError = false)
    {
        statusText.Text = message;
        statusText.Foreground = isError ? Brushes.OrangeRed : Brushes.Gainsboro;
    }
}
