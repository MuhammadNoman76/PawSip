using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PawSip.Controls;

/// <summary>Circular progress ring filled with gently waving water.</summary>
public sealed class WaterRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(WaterRing), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(WaterRing), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush TrackBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0xF1, 0xFE)));
    private static readonly Brush InnerBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0xFA, 0xFF)));
    private static readonly Brush BackWave = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0xA9, 0xDD, 0xFD)));
    private static readonly Brush FrontWave = Freeze(new LinearGradientBrush(Color.FromRgb(0x8F, 0xD3, 0xFF), Color.FromRgb(0x5E, 0xA8, 0xF7), 90));
    private static readonly Brush ArcBrush = Freeze(new LinearGradientBrush(Color.FromRgb(0x6C, 0xC5, 0xFF), Color.FromRgb(0x7C, 0x8C, 0xFF), 45));
    private static readonly Brush DoneArcBrush = Freeze(new LinearGradientBrush(Color.FromRgb(0x5C, 0xCF, 0xA9), Color.FromRgb(0x6C, 0xC5, 0xFF), 45));

    private bool _animating;
    private bool _animationsEnabled = true;

    public WaterRing()
    {
        IsVisibleChanged += (_, _) => UpdateWaveAnimation();
        Loaded += (_, _) => UpdateWaveAnimation();
        Unloaded += (_, _) => StopWave();
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            _animationsEnabled = value;
            UpdateWaveAnimation();
        }
    }

    /// <summary>Smoothly moves the fill to a new value.</summary>
    public void AnimateTo(double progress)
    {
        if (!_animationsEnabled)
        {
            BeginAnimation(ProgressProperty, null);
            Progress = progress;
            return;
        }
        BeginAnimation(ProgressProperty, new DoubleAnimation(progress, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private void UpdateWaveAnimation()
    {
        if (IsVisible && IsLoaded && _animationsEnabled)
        {
            if (_animating) return;
            var wave = new DoubleAnimation(0, Math.PI * 2, TimeSpan.FromSeconds(3.2)) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(wave, 30);
            BeginAnimation(PhaseProperty, wave);
            _animating = true;
        }
        else
        {
            StopWave();
        }
    }

    private void StopWave()
    {
        BeginAnimation(PhaseProperty, null);
        _animating = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var thickness = size * 0.07;
        var radius = size / 2 - thickness / 2 - 2;
        var progress = Math.Max(0, Progress);

        // Track + progress arc
        dc.DrawEllipse(null, new Pen(TrackBrush, thickness), center, radius, radius);
        var arc = Math.Min(progress, 0.9999);
        if (arc > 0.002)
        {
            var pen = new Pen(progress >= 1 ? DoneArcBrush : ArcBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, pen, Arc(center, radius, arc));
        }

        // Inner water
        var inner = radius - thickness * 0.95;
        var innerGeometry = new EllipseGeometry(center, inner, inner);
        dc.DrawGeometry(InnerBrush, null, innerGeometry);
        dc.PushClip(innerGeometry);
        var fill = Math.Min(progress, 1.0);
        var level = center.Y + inner - 2 * inner * (0.04 + 0.92 * fill);
        var amplitude = inner * 0.05;
        dc.DrawGeometry(BackWave, null, Wave(center.X - inner, center.X + inner, level, amplitude, Phase + 1.6, center.Y + inner, 1.3));
        dc.DrawGeometry(FrontWave, null, Wave(center.X - inner, center.X + inner, level + amplitude * 0.6, amplitude, Phase, center.Y + inner, 1.0));
        dc.Pop();
    }

    private static Geometry Arc(Point c, double r, double fraction)
    {
        var start = new Point(c.X, c.Y - r);
        var angle = fraction * 2 * Math.PI - Math.PI / 2;
        var end = new Point(c.X + r * Math.Cos(angle), c.Y + r * Math.Sin(angle));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    private static Geometry Wave(double left, double right, double level, double amplitude, double phase, double bottom, double frequency)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(left, bottom), true, true);
            var width = right - left;
            const int steps = 48;
            for (var i = 0; i <= steps; i++)
            {
                var x = left + width * i / steps;
                var y = level + amplitude * Math.Sin(frequency * 2 * Math.PI * i / steps + phase);
                ctx.LineTo(new Point(x, y), true, false);
            }
            ctx.LineTo(new Point(right, bottom), true, false);
        }
        g.Freeze();
        return g;
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
