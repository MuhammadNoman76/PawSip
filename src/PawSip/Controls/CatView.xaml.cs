using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PawSip.Controls;

public enum CatMood { Idle, Wave, Walk, Sleep, Drink, Happy }

/// <summary>The animated vector cat. All animation runs at 30 FrameRate and stops when the cat isn't on screen.</summary>
public partial class CatView : UserControl
{
    private readonly Random _rng = new();
    private readonly DispatcherTimer _blinkTimer = new();
    private readonly DispatcherTimer _fidgetTimer = new();
    private readonly DispatcherTimer _talkTimer = new() { Interval = TimeSpan.FromMilliseconds(130) };
    private readonly List<(Animatable Target, DependencyProperty Property)> _animated = new();
    private int _talkSteps;
    private bool _animations = true;
    private bool _loaded;

    public CatView()
    {
        InitializeComponent();

        _blinkTimer.Tick += (_, _) =>
        {
            Blink();
            _blinkTimer.Interval = TimeSpan.FromSeconds(2.5 + _rng.NextDouble() * 3.5);
        };
        _fidgetTimer.Tick += (_, _) =>
        {
            Fidget();
            _fidgetTimer.Interval = TimeSpan.FromSeconds(5 + _rng.NextDouble() * 6);
        };
        _talkTimer.Tick += (_, _) =>
        {
            var open = _talkSteps % 2 == 0;
            MouthOpen.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            MouthClosed.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            if (++_talkSteps >= 6)
            {
                _talkTimer.Stop();
                ApplyMouth();
            }
        };

        Loaded += (_, _) =>
        {
            _loaded = true;
            SetMood(Mood);
        };
        Unloaded += (_, _) =>
        {
            _loaded = false;
            StopAll();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (!_loaded) return;
            if (IsVisible) SetMood(Mood);
            else StopAll();
        };
    }

    /// <summary>Raised when the drinking animation has emptied the glass.</summary>
    public event Action? DrinkFinished;

    public CatMood Mood { get; private set; } = CatMood.Idle;

    /// <summary>Animation frame rate - the always-visible desktop widget uses a lower one to save CPU.</summary>
    public int FrameRate { get; set; } = 30;

    public bool AnimationsEnabled
    {
        get => _animations;
        set
        {
            if (_animations == value) return;
            _animations = value;
            if (_loaded) SetMood(Mood);
        }
    }

    // =====================================================================================

    public void SetMood(CatMood mood)
    {
        Mood = mood;
        StopAll();
        if (!_loaded || !IsVisible) return;

        switch (mood)
        {
            case CatMood.Idle:
                Breathe(2.6, 1.025);
                Tail(1.8, -7, 9);
                StartIdleTimers();
                break;

            case CatMood.Wave:
                WaveArm.Visibility = Visibility.Visible;
                PawRight.Visibility = Visibility.Collapsed;
                HeadTilt.Angle = -5;
                Breathe(2.4, 1.025);
                Tail(1.1, -10, 12);
                Animate(WaveRotate, RotateTransform.AngleProperty, Loop(-16, 14, 0.3));
                StartIdleTimers();
                break;

            case CatMood.Walk:
                var hop = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
                hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                hop.KeyFrames.Add(new EasingDoubleKeyFrame(-10, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.17)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
                hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.34)), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
                Animate(CatMove, TranslateTransform.YProperty, hop);
                Animate(CatTilt, RotateTransform.AngleProperty, Loop(-3, 3, 0.34));
                Animate(PawLeftMove, TranslateTransform.YProperty, Loop(0, -7, 0.17));
                Animate(PawRightMove, TranslateTransform.YProperty, Loop(-7, 0, 0.17));
                Animate(ShadowScale, ScaleTransform.ScaleXProperty, Loop(1, 0.86, 0.17));
                Tail(0.5, -8, 12);
                StartIdleTimers(blinkOnly: true);
                break;

            case CatMood.Sleep:
                EyesOpen.Visibility = Visibility.Collapsed;
                EyesSleep.Visibility = Visibility.Visible;
                HeadTilt.Angle = 7;
                HeadBob.Y = 5;
                Breathe(3.6, 1.04);
                Tail(4, -3, 3);
                Zzz.Visibility = Visibility.Visible;
                SleepZ(Z1, Z1Move, 0);
                SleepZ(Z2, Z2Move, 0.8);
                SleepZ(Z3, Z3Move, 1.6);
                break;

            case CatMood.Drink:
                Glass.Visibility = Visibility.Visible;
                PawLeft.Visibility = Visibility.Collapsed;
                PawRight.Visibility = Visibility.Collapsed;
                EyesOpen.Visibility = Visibility.Collapsed;
                EyesHappy.Visibility = Visibility.Visible;
                HeadBob.Y = 4;
                Tail(0.9, -6, 10);
                if (_animations)
                {
                    var drink = new DoubleAnimation(0, 30, TimeSpan.FromSeconds(2.4)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                    drink.Completed += (_, _) =>
                    {
                        if (Mood == CatMood.Drink) DrinkFinished?.Invoke();
                    };
                    Animate(WaterLevel, TranslateTransform.YProperty, drink, limitFps: false);
                    Animate(Bubble1, TranslateTransform.YProperty, new DoubleAnimation(0, -26, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = RepeatBehavior.Forever });
                    Animate(Bubble2, TranslateTransform.YProperty, new DoubleAnimation(0, -30, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(0.4) });
                    Animate(BlushScale, ScaleTransform.ScaleXProperty, Loop(1, 1.12, 0.4));
                    Animate(BlushScale, ScaleTransform.ScaleYProperty, Loop(1, 1.12, 0.4));
                    Animate(HeadBob, TranslateTransform.YProperty, Loop(4, 6, 0.4));
                }
                else
                {
                    After(1.2, () => { if (Mood == CatMood.Drink) DrinkFinished?.Invoke(); });
                }
                break;

            case CatMood.Happy:
                EyesOpen.Visibility = Visibility.Collapsed;
                EyesHappy.Visibility = Visibility.Visible;
                MouthClosed.Visibility = Visibility.Collapsed;
                MouthOpen.Visibility = Visibility.Visible;
                Tail(0.35, -12, 14);
                if (_animations)
                {
                    Jump();
                    HeartsBurst(repeat: true);
                }
                break;
        }
    }

    /// <summary>Opens and closes the mouth a few times ("meow!").</summary>
    public void Meow()
    {
        if (!_animations || Mood is CatMood.Sleep or CatMood.Drink) return;
        _talkSteps = 0;
        _talkTimer.Stop();
        _talkTimer.Start();
    }

    /// <summary>A short happy reaction (being petted) that returns to the current mood.</summary>
    public void Purr()
    {
        if (!_animations || Mood is CatMood.Drink or CatMood.Walk) return;
        var mood = Mood;
        EyesOpen.Visibility = Visibility.Collapsed;
        EyesSleep.Visibility = Visibility.Collapsed;
        EyesHappy.Visibility = Visibility.Visible;
        HeartsBurst(repeat: false);
        Animate(BlushScale, ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.25, TimeSpan.FromSeconds(0.25)) { AutoReverse = true });
        Animate(BlushScale, ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.25, TimeSpan.FromSeconds(0.25)) { AutoReverse = true });
        After(1.4, () =>
        {
            if (Mood == mood && mood != CatMood.Happy) SetMood(mood == CatMood.Sleep ? CatMood.Idle : mood);
        });
    }

    // =====================================================================================

    private void StopAll()
    {
        _blinkTimer.Stop();
        _fidgetTimer.Stop();
        _talkTimer.Stop();
        foreach (var (target, property) in _animated) target.BeginAnimation(property, null);
        _animated.Clear();
        foreach (var heart in new[] { Heart1, Heart2, Heart3 }) heart.BeginAnimation(OpacityProperty, null);
        foreach (var z in new[] { Z1, Z2, Z3 }) z.BeginAnimation(OpacityProperty, null);

        CatSquash.ScaleX = CatSquash.ScaleY = 1;
        CatTilt.Angle = 0;
        CatMove.X = CatMove.Y = 0;
        TailRotate.Angle = 0;
        BodyBreath.ScaleY = 1;
        PawLeftMove.Y = PawRightMove.Y = 0;
        HeadTilt.Angle = 0;
        HeadBob.Y = 0;
        EarLeftRotate.Angle = EarRightRotate.Angle = 0;
        EyesBlink.ScaleY = 1;
        EyesLook.X = 0;
        BlushScale.ScaleX = BlushScale.ScaleY = 1;
        WaveRotate.Angle = 0;
        WaterLevel.Y = 0;
        Bubble1.Y = Bubble2.Y = 0;
        ShadowScale.ScaleX = ShadowScale.ScaleY = 1;

        EyesOpen.Visibility = Visibility.Visible;
        EyesHappy.Visibility = Visibility.Collapsed;
        EyesSleep.Visibility = Visibility.Collapsed;
        MouthClosed.Visibility = Visibility.Visible;
        MouthOpen.Visibility = Visibility.Collapsed;
        WaveArm.Visibility = Visibility.Collapsed;
        PawLeft.Visibility = Visibility.Visible;
        PawRight.Visibility = Visibility.Visible;
        Glass.Visibility = Visibility.Collapsed;
        Zzz.Visibility = Visibility.Collapsed;
        foreach (var heart in new[] { Heart1, Heart2, Heart3 }) heart.Opacity = 0;
    }

    private void ApplyMouth()
    {
        var open = Mood == CatMood.Happy;
        MouthOpen.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        MouthClosed.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
    }

    private void StartIdleTimers(bool blinkOnly = false)
    {
        if (!_animations) return;
        _blinkTimer.Interval = TimeSpan.FromSeconds(1.2 + _rng.NextDouble() * 2);
        _blinkTimer.Start();
        if (blinkOnly) return;
        _fidgetTimer.Interval = TimeSpan.FromSeconds(4 + _rng.NextDouble() * 4);
        _fidgetTimer.Start();
    }

    private void Blink()
    {
        if (EyesOpen.Visibility != Visibility.Visible) return;
        var blink = new DoubleAnimationUsingKeyFrames();
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(0.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        if (_rng.Next(5) == 0) // sometimes a double blink
        {
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(0.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
        }
        Animate(EyesBlink, ScaleTransform.ScaleYProperty, blink, limitFps: false);
    }

    private void Fidget()
    {
        switch (_rng.Next(3))
        {
            case 0: // ear twitch
                var ear = _rng.Next(2) == 0 ? EarLeftRotate : EarRightRotate;
                var angle = ear == EarLeftRotate ? -12 : 12;
                Animate(ear, RotateTransform.AngleProperty, new DoubleAnimation(0, angle, TimeSpan.FromMilliseconds(90)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(2) });
                break;
            case 1: // look around
                var look = new DoubleAnimationUsingKeyFrames();
                look.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                look.KeyFrames.Add(new EasingDoubleKeyFrame(-3.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.25)), new SineEase()));
                look.KeyFrames.Add(new EasingDoubleKeyFrame(-3.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.0))));
                look.KeyFrames.Add(new EasingDoubleKeyFrame(3.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.3)), new SineEase()));
                look.KeyFrames.Add(new EasingDoubleKeyFrame(3.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.0))));
                look.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.3)), new SineEase()));
                Animate(EyesLook, TranslateTransform.XProperty, look);
                break;
            default: // head tilt
                if (Mood != CatMood.Idle) return;
                Animate(HeadTilt, RotateTransform.AngleProperty, new DoubleAnimation(0, _rng.Next(2) == 0 ? -6 : 6, TimeSpan.FromSeconds(0.4))
                {
                    AutoReverse = true,
                    BeginTime = TimeSpan.Zero,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });
                break;
        }
    }

    private void Breathe(double seconds, double amount) =>
        Animate(BodyBreath, ScaleTransform.ScaleYProperty, Loop(1, amount, seconds / 2));

    private void Tail(double seconds, double from, double to) =>
        Animate(TailRotate, RotateTransform.AngleProperty, Loop(from, to, seconds / 2));

    private void SleepZ(UIElement z, TranslateTransform move, double delay)
    {
        if (!_animations)
        {
            z.Opacity = 1;
            return;
        }
        var fade = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(delay), Duration = TimeSpan.FromSeconds(2.4) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.4))));
        Timeline.SetDesiredFrameRate(fade, FrameRate);
        z.Opacity = 0;
        z.BeginAnimation(OpacityProperty, fade);
        Animate(move, TranslateTransform.YProperty, new DoubleAnimation(6, -14, TimeSpan.FromSeconds(2.4)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(delay) });
    }

    private void Jump()
    {
        var jump = new DoubleAnimationUsingKeyFrames { RepeatBehavior = new RepeatBehavior(2) };
        jump.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        jump.KeyFrames.Add(new EasingDoubleKeyFrame(-22, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.22)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        jump.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.44)), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        jump.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));
        Animate(CatMove, TranslateTransform.YProperty, jump, limitFps: false);

        var squash = new DoubleAnimationUsingKeyFrames { RepeatBehavior = new RepeatBehavior(2) };
        squash.KeyFrames.Add(new EasingDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        squash.KeyFrames.Add(new EasingDoubleKeyFrame(1.04, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2))));
        squash.KeyFrames.Add(new EasingDoubleKeyFrame(0.95, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.46))));
        squash.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));
        Animate(CatSquash, ScaleTransform.ScaleYProperty, squash, limitFps: false);

        var shadow = new DoubleAnimationUsingKeyFrames { RepeatBehavior = new RepeatBehavior(2) };
        shadow.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        shadow.KeyFrames.Add(new EasingDoubleKeyFrame(0.7, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.22))));
        shadow.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.44))));
        Animate(ShadowScale, ScaleTransform.ScaleXProperty, shadow, limitFps: false);
    }

    private void HeartsBurst(bool repeat)
    {
        var hearts = new[] { (Heart1, Heart1Move, Heart1Scale), (Heart2, Heart2Move, Heart2Scale), (Heart3, Heart3Move, Heart3Scale) };
        for (var i = 0; i < hearts.Length; i++)
        {
            var (heart, move, scale) = hearts[i];
            var begin = TimeSpan.FromSeconds(i * 0.18);
            var duration = TimeSpan.FromSeconds(1.3);
            RepeatBehavior repeatBehavior = repeat ? RepeatBehavior.Forever : new RepeatBehavior(1);

            var fade = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = repeat ? TimeSpan.FromSeconds(1.9) : duration, RepeatBehavior = repeatBehavior };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.25))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(duration)));
            Timeline.SetDesiredFrameRate(fade, FrameRate);
            heart.BeginAnimation(OpacityProperty, fade);

            Animate(move, TranslateTransform.YProperty, new DoubleAnimation(4, -34, duration)
            {
                BeginTime = begin,
                RepeatBehavior = repeatBehavior,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
            var grow = new DoubleAnimation(0.5, 1.15, duration) { BeginTime = begin, RepeatBehavior = repeatBehavior };
            Animate(scale, ScaleTransform.ScaleXProperty, grow);
            Animate(scale, ScaleTransform.ScaleYProperty, grow.Clone());
        }
    }

    private void Animate(Animatable target, DependencyProperty property, AnimationTimeline animation, bool limitFps = true)
    {
        if (!_animations) return;
        if (limitFps) Timeline.SetDesiredFrameRate(animation, FrameRate);
        target.BeginAnimation(property, animation);
        _animated.Add((target, property));
    }

    private static DoubleAnimation Loop(double from, double to, double halfSeconds) =>
        new(from, to, TimeSpan.FromSeconds(halfSeconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };

    private void After(double seconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }
}
