using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PawSip.Views;

/// <summary>The "Remind me in …" choices shown after pressing "Later".</summary>
public static class SnoozePicker
{
    public static readonly int[] Choices = { 5, 10, 15, 30, 60 };

    public static string Label(int minutes) => minutes >= 60 ? $"{minutes / 60} hour" : $"{minutes} min";

    public static FrameworkElement Build(int defaultMinutes, Action<int> onPick)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Remind me again in…",
            FontSize = 12.5,
            Foreground = (Brush)Application.Current.FindResource("Ink2"),
            Margin = new Thickness(0, 0, 0, 8),
        });
        var chips = new WrapPanel();
        foreach (var minutes in Choices)
        {
            var isDefault = minutes == defaultMinutes;
            var button = new Button
            {
                Style = (Style)Application.Current.FindResource("SoftButton"),
                Content = Label(minutes),
                Height = 32,
                FontSize = 12.5,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 6, 6),
                Background = (Brush)Application.Current.FindResource(isDefault ? "WaterTint" : "Field"),
                Foreground = (Brush)Application.Current.FindResource(isDefault ? "WaterDeep" : "Ink2"),
            };
            button.Click += (_, _) => onPick(minutes);
            chips.Children.Add(button);
        }
        panel.Children.Add(chips);
        return panel;
    }
}
