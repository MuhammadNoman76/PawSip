using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PawSip.Core;

namespace PawSip.Controls;

/// <summary>Calendar of a month; each day is tinted by how much of the goal was reached.</summary>
public sealed class MonthHeatmap : FrameworkElement
{
    private static readonly Typeface Face = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Color Low = Color.FromRgb(0xEE, 0xF4, 0xFF);
    private static readonly Color High = Color.FromRgb(0x6C, 0xA9, 0xFF);
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0xA3, 0xA9, 0xC2));
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x2E, 0x33, 0x52));
    private static readonly Brush Empty = new SolidColorBrush(Color.FromRgb(0xF6, 0xF7, 0xFC));
    private static readonly Pen TodayPen = new(new SolidColorBrush(Color.FromRgb(0xFF, 0x8D, 0xB3)), 2.2);

    private IReadOnlyList<DayStat> _days = Array.Empty<DayStat>();
    private DateTime _month = DateTime.Today;

    public void SetMonth(DateTime month, IReadOnlyList<DayStat> days)
    {
        _month = new DateTime(month.Year, month.Month, 1);
        _days = days;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0) return;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        const double headerHeight = 24;
        const double gap = 6;
        var cell = (ActualWidth - gap * 6) / 7;
        var rowHeight = Math.Min(cell, (ActualHeight - headerHeight - gap * 5) / 6);

        var firstDay = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        for (var c = 0; c < 7; c++)
        {
            var name = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(firstDay + c) % 7];
            var t = Text(name[..Math.Min(2, name.Length)], 11.5, Muted, dpi);
            dc.DrawText(t, new Point(c * (cell + gap) + (cell - t.Width) / 2, 2));
        }

        var offset = ((int)_month.DayOfWeek - firstDay + 7) % 7;
        foreach (var day in _days)
        {
            var index = offset + day.Date.Day - 1;
            var col = index % 7;
            var row = index / 7;
            var rect = new Rect(col * (cell + gap), headerHeight + row * (rowHeight + gap), cell, rowHeight);
            var future = day.Date > DateTime.Today;
            Brush fill;
            if (future || day.Ml <= 0) fill = Empty;
            else
            {
                var f = MathEx.Clamp(day.Fraction, 0.12, 1);
                fill = new SolidColorBrush(Lerp(Low, High, f));
            }
            dc.DrawRoundedRectangle(fill, day.Date == DateTime.Today ? TodayPen : null, rect, 10, 10);

            var strong = !future && day.Fraction >= 0.6;
            var label = Text(day.Date.Day.ToString(CultureInfo.CurrentCulture), 12.5, strong ? Brushes.White : future ? Muted : Ink, dpi);
            dc.DrawText(label, new Point(rect.X + (rect.Width - label.Width) / 2, rect.Y + (rect.Height - label.Height) / 2));
        }
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static FormattedText Text(string s, double size, Brush brush, double dpi) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, brush, dpi);
}
