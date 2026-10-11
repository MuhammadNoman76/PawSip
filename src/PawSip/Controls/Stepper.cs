using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace PawSip.Controls;

/// <summary>[ - ]  value  [ + ] pill used for numbers and times in settings.</summary>
public sealed class Stepper : Border
{
    private readonly TextBlock _text;
    private double _value;

    public Stepper()
    {
        CornerRadius = new CornerRadius(19);
        Background = (Brush)Application.Current.FindResource("Field");
        Padding = new Thickness(3);
        Height = 38;
        HorizontalAlignment = HorizontalAlignment.Left;

        var minus = MakeButton("Icon.Minus", -1);
        var plus = MakeButton("Icon.Plus", +1);
        _text = new TextBlock
        {
            MinWidth = 92,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Ink"),
            Margin = new Thickness(6, 0, 6, 0),
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(minus);
        panel.Children.Add(_text);
        panel.Children.Add(plus);
        Child = panel;
    }

    public double Minimum { get; set; }
    public double Maximum { get; set; } = 100;
    public double Step { get; set; } = 1;
    public bool Wrap { get; set; }
    public Func<double, string> Format { get; set; } = v => v.ToString("0");

    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, Minimum, Maximum);
            _text.Text = Format(_value);
        }
    }

    public event Action<double>? ValueChanged;

    private Button MakeButton(string icon, int direction)
    {
        var button = new Button
        {
            Style = (Style)Application.Current.FindResource("IconButton"),
            Width = 32,
            Height = 32,
            Background = Brushes.White,
            Content = new Path
            {
                Data = (Geometry)Application.Current.FindResource(icon),
                Style = (Style)Application.Current.FindResource("Icon"),
                Width = 14,
                Height = 14,
                Fill = (Brush)Application.Current.FindResource("WaterDeep"),
            },
        };
        button.Click += (_, _) =>
        {
            var next = _value + direction * Step;
            if (Wrap)
            {
                var range = Maximum - Minimum + Step;
                if (next > Maximum) next -= range;
                if (next < Minimum) next += range;
            }
            next = Math.Clamp(next, Minimum, Maximum);
            if (Math.Abs(next - _value) < 1e-9) return;
            Value = next;
            ValueChanged?.Invoke(_value);
        };
        return button;
    }
}
