using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PawSip.Controls;
using PawSip.Core;
using PawSip.Interop;

namespace PawSip.Views;

/// <summary>
/// The cat that walks onto the desktop with a reminder. Never steals focus, can be dragged anywhere,
/// naps if ignored and leaves on its own.
/// </summary>
public partial class CatReminderWindow : Window
{
    private const double WalkDistance = 440;
    private readonly DataStore _store;
    private readonly Hydration _hydration;
    private readonly Settings _settings;
    private readonly string _message;
    private readonly DispatcherTimer _napTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly DispatcherTimer _leaveTimer = new();
    private bool _arrived;
    private bool _responded;
    private bool _leaving;
    private bool _napping;
    private bool _fromRight = true;

    public CatReminderWindow(DataStore store, Hydration hydration, string message)
    {
        _store = store;
        _hydration = hydration;
        _settings = store.Settings;
        _message = message;
        InitializeComponent();

        var catSize = _settings.CatSize switch { CatSize.Small => 115.0, CatSize.Large => 190.0, _ => 150.0 };
        Cat.Width = catSize;
        Cat.Height = catSize * 1.02;
        Height = catSize * 1.02 + 230;
        Cat.AnimationsEnabled = _settings.Animations;

        MessageText.Text = message;
        UpdateProgress(animate: false);

        DrankButton.Click += (_, _) => OnDrank();
        LaterButton.Click += (_, _) => OnLater();
        DismissButton.Click += (_, _) => OnDismiss();
        Cat.MouseLeftButtonDown += OnCatMouseDown;
        Cat.DrinkFinished += () =>
        {
            Cat.SetMood(CatMood.Happy);
            After(1.9, Leave);
        };

        _napTimer.Tick += (_, _) =>
        {
            _napTimer.Stop();
            if (!_responded) Nap();
        };
        _leaveTimer.Tick += (_, _) =>
        {
            _leaveTimer.Stop();
            if (_responded) return;
            Respond(ReminderResponse.Ignored);
            Leave();
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Native.MakeNonActivating(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        };
        PlaceAtStart();
        Loaded += (_, _) => Arrive();
        Closed += (_, _) =>
        {
            _napTimer.Stop();
            _leaveTimer.Stop();
            if (!_responded) Respond(ReminderResponse.Ignored);
        };
    }

    /// <summary>The answer, plus the chosen snooze minutes for "Remind me later".</summary>
    public event Action<ReminderResponse, int>? Responded;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(Native.MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    // ------------------------------------------------------------------ position

    private Point Target()
    {
        var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (_store.Data.CatLeft is { } x && _store.Data.CatTop is { } y)
        {
            var saved = new Rect(x, y, Width, Height);
            if (virtualScreen.Contains(new Point(saved.Left + Width / 2, saved.Top + Height * 0.75))) return new Point(x, y);
        }
        var work = SystemParameters.WorkArea;
        return new Point(work.Right - Width - 12, work.Bottom - Height + 6);
    }

    private void PlaceAtStart()
    {
        var target = Target();
        var center = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2;
        _fromRight = target.X + Width / 2 >= center;
        Top = target.Y;
        Left = _settings.Animations ? target.X + (_fromRight ? WalkDistance : -WalkDistance) : target.X;
        Opacity = _settings.Animations ? 0 : 1;
    }

    // ------------------------------------------------------------------ arrival / leaving

    private void Arrive()
    {
        var target = Target();
        if (!_settings.Animations)
        {
            Arrived();
            return;
        }

        Cat.SetMood(CatMood.Walk);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));
        var walk = new DoubleAnimation(target.X, TimeSpan.FromSeconds(1.7)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } };
        walk.Completed += (_, _) =>
        {
            var left = Left;
            BeginAnimation(LeftProperty, null); // release the animated value so dragging works
            Left = left;
            Arrived();
        };
        BeginAnimation(LeftProperty, walk);
    }

    private void Arrived()
    {
        _arrived = true;
        Cat.SetMood(CatMood.Wave);
        Cat.Meow();
        Sounds.Play(_settings.Sound, _settings.Volume);
        ShowBubble();
        After(2.6, () =>
        {
            if (!_responded && !_napping && Cat.Mood == CatMood.Wave) Cat.SetMood(CatMood.Idle);
        });

        _napTimer.Start();
        if (_settings.AutoHideMinutes > 0)
        {
            _leaveTimer.Interval = TimeSpan.FromMinutes(_settings.AutoHideMinutes);
            _leaveTimer.Start();
        }
    }

    private void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        _napTimer.Stop();
        _leaveTimer.Stop();
        HideBubble();

        if (!_settings.Animations)
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
            return;
        }

        Cat.SetMood(CatMood.Walk);
        var walk = new DoubleAnimation(Left + (_fromRight ? WalkDistance : -WalkDistance), TimeSpan.FromSeconds(1.5)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } };
        walk.Completed += (_, _) => Close();
        BeginAnimation(LeftProperty, walk);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromSeconds(1.0) });
    }

    // ------------------------------------------------------------------ bubble

    private void ShowBubble()
    {
        Bubble.Visibility = Visibility.Visible;
        if (!_settings.Animations)
        {
            Bubble.Opacity = 1;
            BubbleScale.ScaleX = BubbleScale.ScaleY = 1;
            return;
        }
        Bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
        var pop = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55 } };
        BubbleScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        BubbleScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void HideBubble()
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
        Bubble.BeginAnimation(OpacityProperty, fade);
        var shrink = new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(200));
        BubbleScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        BubbleScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
    }

    private void UpdateProgress(bool animate)
    {
        var fraction = Math.Clamp(_hydration.Fraction, 0, 1);
        ProgressText.Text = _hydration.GoalReached
            ? $"{_hydration.TodayMl:N0} / {_hydration.GoalMl:N0} ml · goal reached!"
            : $"{_hydration.TodayMl:N0} / {_hydration.GoalMl:N0} ml · {_hydration.RemainingGlasses} {(_hydration.RemainingGlasses == 1 ? "glass" : "glasses")} to go";

        var scale = ProgressFill.RenderTransform as ScaleTransform;
        if (scale == null)
        {
            ProgressFill.HorizontalAlignment = HorizontalAlignment.Stretch;
            ProgressFill.Width = double.NaN;
            ProgressFill.RenderTransformOrigin = new Point(0, 0.5);
            ProgressFill.RenderTransform = scale = new ScaleTransform(fraction, 1);
        }
        if (animate && _settings.Animations)
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fraction, TimeSpan.FromMilliseconds(700)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else
            scale.ScaleX = fraction;
    }

    // ------------------------------------------------------------------ interaction

    private void Respond(ReminderResponse response, int snoozeMinutes = 0)
    {
        if (_responded) return;
        _responded = true;
        Responded?.Invoke(response, snoozeMinutes);
    }

    private void Settle(string message)
    {
        _napTimer.Stop();
        _leaveTimer.Stop();
        _napping = false;
        MessageText.Text = message;
        Actions.Visibility = Visibility.Collapsed;
        DismissButton.Visibility = Visibility.Collapsed;
        if (Bubble.Opacity < 1) ShowBubble();
    }

    private void OnDrank()
    {
        if (_responded) return;
        Respond(ReminderResponse.Drank);
        Settle(Messages.Thank(_hydration.GoalReached));
        UpdateProgress(animate: true);
        if (_settings.Animations) Cat.SetMood(CatMood.Drink); // DrinkFinished -> happy -> leave
        else
        {
            Cat.SetMood(CatMood.Happy);
            After(2.0, Leave);
        }
    }

    private void OnLater()
    {
        if (_responded) return;
        // Let the user pick how long.
        Actions.Visibility = Visibility.Collapsed;
        SnoozeHost.Content = SnoozePicker.Build(_settings.SnoozeMinutes, SnoozeFor);
        SnoozeHost.Visibility = Visibility.Visible;
    }

    private void SnoozeFor(int minutes)
    {
        if (_responded) return;
        Respond(ReminderResponse.Later, minutes);
        SnoozeHost.Visibility = Visibility.Collapsed;
        Settle($"Okay! I'll be back in {SnoozePicker.Label(minutes)}~");
        Cat.SetMood(CatMood.Wave);
        Cat.Meow();
        After(1.8, Leave);
    }

    private void OnDismiss()
    {
        if (_responded) return;
        Respond(ReminderResponse.Dismissed);
        Settle($"Okay… I'll check on you again in {_settings.RepeatMinutes} minutes~");
        Cat.SetMood(CatMood.Wave);
        After(1.3, Leave);
    }

    private void Nap()
    {
        _napping = true;
        HideBubble();
        Cat.SetMood(CatMood.Sleep);
    }

    private void Wake()
    {
        _napping = false;
        Cat.SetMood(CatMood.Wave);
        Cat.Meow();
        ShowBubble();
        _napTimer.Start();
        After(2.2, () =>
        {
            if (!_responded && !_napping && Cat.Mood == CatMood.Wave) Cat.SetMood(CatMood.Idle);
        });
    }

    private void OnCatMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_arrived || _leaving) return;
        e.Handled = true;
        var before = new Point(Left, Top);
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (Math.Abs(Left - before.X) > 3 || Math.Abs(Top - before.Y) > 3)
        {
            _store.Data.CatLeft = Left;
            _store.Data.CatTop = Top;
            _store.Save();
            return;
        }

        // A click, not a drag: pet the cat.
        if (_napping)
        {
            Wake();
            return;
        }
        Cat.Purr();
        if (!_responded)
        {
            MessageText.Text = Messages.Pet();
            After(1.6, () =>
            {
                if (!_responded) MessageText.Text = _message;
            });
        }
    }

    private static void After(double seconds, Action action)
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
