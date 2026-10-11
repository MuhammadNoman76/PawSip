using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PawSip.Controls;

public sealed record BarItem(string Label, string Detail, double Value, double Goal, bool Highlight);

/// <summary>Rounded bars that grow in, a dashed goal line and a hover label.</summary>
public sealed class BarChart : FrameworkElement
{
    public static readonly DependencyProperty RevealProperty = DependencyProperty.Register(
        nameof(Reveal), typeof(double), typeof(BarChart), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush MetBrush = Freeze(new LinearGradientBrush(Color.FromRgb(0x7C, 0x8C, 0xFF), Color.FromRgb(0x6C, 0xC5, 0xFF), 90));
    private static readonly Brush PartBrush = Freeze(new LinearGradientBrush(Color.FromRgb(0xC9, 0xC2, 0xFB), Color.FromRgb(0xBF, 0xE3, 0xFF), 90));
    private static readonly Brush EmptyBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xF2, 0xF9)));
    private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xA3, 0xA9, 0xC2)));
    private static readonly Brush StrongLabel = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x33, 0x52)));
    private static readonly Brush TipBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xEE, 0x2E, 0x33, 0x52)));
    private static readonly Pen GoalPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x8D, 0xB3)), 1.6) { DashStyle = new DashStyle(new[] { 3.0, 3.0 }, 0) });
    private static readonly Typeface Face = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private IReadOnlyList<BarItem> _items = Array.Empty<BarItem>();
    private int _hover = -1;

    public BarChart()
    {
        MouseMove += (_, e) => SetHover(HitIndex(e.GetPosition(this)));
        MouseLeave += (_, _) => SetHover(-1);
        Cursor = Cursors.Arrow;
    }

    public double Reveal
    {
        get => (double)GetValue(RevealProperty);
        set => SetValue(RevealProperty, value);
    }

    public bool ShowAllLabels { get; set; } = true;

    public void SetItems(IReadOnlyList<BarItem> items, bool animate)
    {
        _items = items;
        _hover = -1;
        if (animate)
            BeginAnimation(RevealProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else
        {
            BeginAnimation(RevealProperty, null);
            Reveal = 1;
        }
        InvalidateVisual();
    }

    private void SetHover(int index)
    {
        if (index == _hover) return;
        _hover = index;
        InvalidateVisual();
    }

    private const double LabelHeight = 26;
    private const double TopPad = 30;

    private int HitIndex(Point p)
    {
        if (_items.Count == 0) return -1;
        var slot = ActualWidth / _items.Count;
        var i = (int)(p.X / slot);
        return i >= 0 && i < _items.Count ? i : -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight)); // hit-testable
        if (_items.Count == 0 || ActualWidth <= 0) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var chartHeight = ActualHeight - LabelHeight - TopPad;
        var max = Math.Max(1, _items.Max(i => Math.Max(i.Value, i.Goal)) * 1.1);
        var slot = ActualWidth / _items.Count;
        var barWidth = Math.Min(38, slot * 0.58);
        var reveal = Reveal;

        // goal line (use the most common goal)
        var goal = _items.Where(i => i.Goal > 0).Select(i => i.Goal).DefaultIfEmpty(0).Max();
        if (goal > 0)
        {
            var gy = TopPad + chartHeight * (1 - goal / max);
            dc.DrawLine(GoalPen, new Point(0, gy), new Point(ActualWidth, gy));
        }

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var x = slot * i + (slot - barWidth) / 2;
            var bottom = TopPad + chartHeight;

            // background capsule
            dc.DrawRoundedRectangle(EmptyBrush, null, new Rect(x, TopPad, barWidth, chartHeight), barWidth / 2, barWidth / 2);

            var h = chartHeight * Math.Min(1, item.Value / max) * reveal;
            if (h > 0.5)
            {
                h = Math.Max(h, barWidth); // keep it pill-shaped
                h = Math.Min(h, chartHeight);
                var brush = item.Goal > 0 && item.Value >= item.Goal ? MetBrush : PartBrush;
                dc.PushOpacity(_hover == -1 || _hover == i ? 1 : 0.55);
                dc.DrawRoundedRectangle(brush, null, new Rect(x, bottom - h, barWidth, h), barWidth / 2, barWidth / 2);
                dc.Pop();
            }

            if (ShowAllLabels || i % 5 == 0 || i == _items.Count - 1 || item.Highlight)
            {
                var label = Text(item.Label, 11.5, item.Highlight ? StrongLabel : LabelBrush, dpi);
                dc.DrawText(label, new Point(slot * i + (slot - label.Width) / 2, bottom + 8));
            }
        }

        if (_hover >= 0)
        {
            var item = _items[_hover];
            var tip = Text(item.Detail, 12, Brushes.White, dpi);
            var w = tip.Width + 20;
            var cx = slot * _hover + slot / 2;
            var tx = MathEx.Clamp(cx - w / 2, 0, Math.Max(0, ActualWidth - w));
            var rect = new Rect(tx, 0, w, 24);
            dc.DrawRoundedRectangle(TipBrush, null, rect, 8, 8);
            dc.DrawText(tip, new Point(tx + 10, 4));
        }
    }

    private static FormattedText Text(string s, double size, Brush brush, double dpi) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, brush, dpi);

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
