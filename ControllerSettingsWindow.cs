using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;

internal sealed class ControllerSettingsWindow : Window
{
    private static readonly Key[] AvailableKeys = Enum.GetValues<Key>()
        .Where(key => key != Key.None && key is not (Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin))
        .ToArray();
    private readonly Dictionary<ControllerButton, ComboBox> selectors = new();

    public Dictionary<ControllerButton, Key> Bindings { get; private set; }

    public ControllerSettingsWindow(Dictionary<ControllerButton, Key> bindings)
    {
        Bindings = new(bindings);
        Title = "Controller Settings";
        Width = 360;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        StackPanel rows = new() { Margin = new Thickness(18), Spacing = 10 };
        rows.Children.Add(new TextBlock
        {
            Text = "Player 1 keyboard bindings",
            FontSize = 18,
            Margin = new Thickness(0, 0, 0, 4)
        });

        foreach (ControllerButton button in Enum.GetValues<ControllerButton>())
        {
            Grid row = new();
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            TextBlock label = new() { Text = button.ToString(), VerticalAlignment = VerticalAlignment.Center };
            ComboBox selector = new()
            {
                ItemsSource = AvailableKeys,
                SelectedItem = bindings[button],
                MinWidth = 140
            };
            selectors[button] = selector;
            Grid.SetColumn(selector, 1);
            row.Children.Add(label);
            row.Children.Add(selector);
            rows.Children.Add(row);
        }

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Button cancel = new() { Content = "Cancel" };
        cancel.Click += (_, _) => Close(false);
        Button save = new() { Content = "Save" };
        save.Click += (_, _) =>
        {
            Bindings = selectors.ToDictionary(pair => pair.Key, pair => (Key)pair.Value.SelectedItem!);
            Close(true);
        };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        rows.Children.Add(actions);
        Content = rows;
    }

    public Task<bool> ShowForOwner(Window owner) => ShowDialog<bool>(owner);
}
