using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

public sealed class YanesEmuApp : Application
{
    public override void Initialize()
    {
        Name = "YANESEmu";
        Styles.Add(new FluentTheme());

        NativeMenuItem about = new() { Header = "About YANESEmu…" };
        about.Click += (_, _) =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is MainWindow window)
                window.ShowAbout();
        };
        NativeMenu applicationMenu = new();
        applicationMenu.Items.Add(about);
        NativeMenu.SetMenu(this, applicationMenu);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
