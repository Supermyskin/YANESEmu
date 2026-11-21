using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

internal sealed class AudioSettingsWindow : Window
{
    private readonly Slider volumeSlider;
    private readonly CheckBox muteCheckBox;

    public float Volume => (float)volumeSlider.Value / 100f;
    public bool Muted => muteCheckBox.IsChecked == true;

    public AudioSettingsWindow(float volume, bool muted)
    {
        Title = "Audio Settings";
        Width = 360;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        StackPanel layout = new() { Margin = new Thickness(18), Spacing = 12 };
        layout.Children.Add(new TextBlock { Text = "Master volume", FontSize = 18 });
        volumeSlider = new Slider { Minimum = 0, Maximum = 100, Value = Math.Clamp(volume, 0, 1) * 100 };
        layout.Children.Add(volumeSlider);
        muteCheckBox = new CheckBox { Content = "Mute audio", IsChecked = muted };
        layout.Children.Add(muteCheckBox);
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = new() { Content = "Cancel" };
        cancel.Click += (_, _) => Close(false);
        Button save = new() { Content = "Save" };
        save.Click += (_, _) => Close(true);
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        layout.Children.Add(actions);
        Content = layout;
    }

    public Task<bool> ShowForOwner(Window owner) => ShowDialog<bool>(owner);
}
