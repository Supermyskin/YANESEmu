using System;
using System.IO;
using System.Runtime.InteropServices;
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
    private readonly WriteableBitmap bitmap = new(
        new PixelSize(FrameWidth, FrameHeight), new Vector(96, 96),
        PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private readonly Image frameImage;
    private readonly TextBlock statusText;
    private readonly DispatcherTimer frameTimer;
    private readonly byte[] bgraFrame = new byte[FrameWidth * FrameHeight * 4];
    private string? currentRomPath;
    private Task? runTask;
    private volatile bool isRunning;
    private volatile bool isPaused;

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
            Foreground = Brushes.Gainsboro
        };

        Menu menu = BuildMenu();
        Border displayBorder = new()
        {
            Child = frameImage,
            Background = Brushes.Black,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            ClipToBounds = true
        };
        Grid.SetRow(menu, 0);
        Grid.SetRow(displayBorder, 1);
        Grid.SetRow(statusText, 2);
        Grid layout = new();
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.Children.Add(menu);
        layout.Children.Add(displayBorder);
        layout.Children.Add(statusText);
        Content = layout;

        emulator.Diagnostic += message => Dispatcher.UIThread.Post(() => SetStatus(message));
        frameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        frameTimer.Tick += (_, _) => UpdateFrame();
        frameTimer.Start();
        Closed += (_, _) =>
        {
            isRunning = false;
            frameTimer.Stop();
            bitmap.Dispose();
        };
    }

    private Menu BuildMenu()
    {
        MenuItem open = new() { Header = "Open ROM…" };
        open.Click += async (_, _) => await OpenRomAsync();
        MenuItem reset = new() { Header = "Reset", InputGesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.R, Avalonia.Input.KeyModifiers.Meta) };
        reset.Click += async (_, _) => await ResetRomAsync();
        MenuItem pause = new() { Header = "Pause" };
        pause.Click += (_, _) => TogglePause(pause);
        MenuItem quit = new() { Header = "Quit" };
        quit.Click += (_, _) => Close();
        MenuItem fileMenu = new() { Header = "File" };
        fileMenu.Items.Add(open);
        fileMenu.Items.Add(reset);
        fileMenu.Items.Add(pause);
        fileMenu.Items.Add(new Separator());
        fileMenu.Items.Add(quit);
        Menu menu = new();
        menu.Items.Add(fileMenu);
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
        string? path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null) await LoadRomAsync(path);
    }

    private async Task LoadRomAsync(string path)
    {
        await StopEmulationAsync();
        if (!emulator.Reset(path)) return;
        currentRomPath = path;
        Title = $"YANESEmu — {Path.GetFileName(path)}";
        isPaused = false;
        SetStatus($"Loaded {Path.GetFileName(path)}. Choose File → Pause to pause emulation.");
        StartEmulation();
    }

    private async Task ResetRomAsync()
    {
        if (currentRomPath is not null) await LoadRomAsync(currentRomPath);
    }

    private async Task StopEmulationAsync()
    {
        isRunning = false;
        if (runTask is not null)
        {
            await runTask;
            runTask = null;
        }
    }

    private void StartEmulation()
    {
        isRunning = true;
        runTask = Task.Run(async () =>
        {
            while (isRunning && !emulator.CPU_Halted)
            {
                if (isPaused)
                {
                    await Task.Delay(10);
                    continue;
                }

                for (int i = 0; i < 2500 && isRunning && !isPaused && !emulator.CPU_Halted; i++)
                    emulator.Step();
                await Task.Yield();
            }

            if (emulator.CPU_Halted)
                Dispatcher.UIThread.Post(() => SetStatus("Emulation stopped because the CPU halted. Check the ROM or diagnostic message."));
        });
    }

    private void TogglePause(MenuItem item)
    {
        if (currentRomPath is null) return;
        isPaused = !isPaused;
        item.Header = isPaused ? "Resume" : "Pause";
        SetStatus(isPaused ? "Paused." : $"Running {Path.GetFileName(currentRomPath)}.");
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

    private void SetStatus(string message) => statusText.Text = message;
}
