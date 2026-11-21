using Avalonia;
using System.Linq;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        YanesEmuApp.TraceEnabled = args.Contains("--trace", System.StringComparer.Ordinal);
        string[] appArgs = args.Where(argument => argument != "--trace").ToArray();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(appArgs);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<YanesEmuApp>()
        .UsePlatformDetect()
        .LogToTrace();
}
