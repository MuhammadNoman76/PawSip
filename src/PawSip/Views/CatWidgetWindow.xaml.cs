using System.Windows;
using System.Windows.Controls;
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
/// The desktop widget: a little cat that lives on the desktop with a water gauge. Hover it to log a glass;
/// at reminder time this cat wakes up and asks you to drink. Naps between reminders so it never distracts.
/// </summary>
public partial class CatWidgetWindow : Window
{
    private readonly App _app;
    private readonly DispatcherTimer _napTimer = new() { Interval = TimeSpan.FromSeconds(90) };
    private readonly DispatcherTimer _fullscreenCheck = new() { Interval = TimeSpan.FromSeconds(2.5) };
    private readonly DispatcherTimer _reminderTimeout = new();
    private readonly DispatcherTimer _bubbleTimer = new();
    private bool _reminding;
    private bool _hiddenForFullscreen;
    private string _reminderMessage = "";

    public CatWidgetWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Cat.FrameRate = 20; // always on screen: keep it light
        Cat.AnimationsEnabled = Settings.Animations;
        Topmost = Settings.WidgetOnTop;
        ApplySize(keepBottom: false);
        PlaceWindow();

        Body.MouseEnter += (_, _) => ShowHoverButtons(true);
        Body.MouseLeave += (_, _) => ShowHoverButtons(false);
        Cat.MouseLeftButtonDown += OnCatMouseDown;
        Pill.MouseLeftButtonUp += (_, _) => _app.ShowMain();
        DrinkButton.Click += (_, _) =>
        {
            if (_reminding) Respond(ReminderResponse.Drank);
            else _app.RecordDrink(Settings.GlassMl);
        };
        OpenButton.Click += (_, _) => _app.ShowMain();
        HideButton.Click += (_, _) => _app.SetWidgetVisible(false);
        UndoButton.Click += (_, _) => Undo();
        DrankButton.Click += (_, _) => Respond(ReminderResponse.Drank);
        LaterButton.Click += (_, _) =>
        {
            Actions.Visibility = Visibility.Collapsed;
            SnoozeHost.Content = SnoozePicker.Build(Settings.SnoozeMinutes, minutes => Respond(ReminderResponse.Later, minutes));
            SnoozeHost.Visibility = Visibility.Visible;
        };
        Body.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            ShowMenu();
        };
        DismissButton.Click += (_, _) =>
        {
            if (_reminding) Respond(ReminderResponse.Dismissed);
            else HideBubble();
        };

        Cat.DrinkFinished += () =>
        {
            Cat.SetMood(CatMood.Happy);
            After(2.0, () =>
            {
                if (!_reminding && Cat.Mood == CatMood.Happy) Cat.SetMood(CatMood.Idle);
                RestartNap();
            });
        };

        _napTimer.Tick += (_, _) =>
        {
            _napTimer.Stop();
            if (!_reminding && !Body.IsMouseOver && Cat.Mood == CatMood.Idle) Cat.SetMood(CatMood.Sleep);
        };
        _fullscreenCheck.Tick += (_, _) => CheckFullscreen();
        _reminderTimeout.Tick += (_, _) =>
        {
            _reminderTimeout.Stop();
            if (_reminding) Respond(ReminderResponse.Ignored);
        };
        _bubbleTimer.Tick += (_, _) =>
        {
            _bubbleTimer.Stop();
            if (!_reminding) HideBubble();
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Native.MakeNonActivating(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        };
        Loaded += (_, _) =>
        {
            _app.Hydration.Changed += UpdateGauge;
            _app.DrinkRecorded += OnDrinkRecorded;
            UpdateGauge();
            Cat.SetMood(CatMood.Idle);
            RestartNap();
            _fullscreenCheck.Start();
        };
        Closed += (_, _) =>
        {
            _app.Hydration.Changed -= UpdateGauge;
            _app.DrinkRecorded -= OnDrinkRecorded;
            _napTimer.Stop();
            _fullscreenCheck.Stop();
            _reminderTimeout.Stop();
            _bubbleTimer.Stop();
            if (_reminding) Respond(ReminderResponse.Ignored);
        };
    }

    /// <summary>The answer to a reminder, plus the chosen snooze minutes for "Remind me later".</summary>
    public event Action<ReminderResponse, int>? Responded;

    private Settings Settings => _app.Store.Settings;

    /// <summary>Can this cat deliver a reminder right now?</summary>
    public bool IsAvailable => IsVisible && !_hiddenForFullscreen;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(Native.MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    // ------------------------------------------------------------------ placement

    private void PlaceWindow()
    {
        var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (_app.Store.Data.WidgetLeft is { } x && _app.Store.Data.WidgetTop is { } y &&
            virtualScreen.Contains(new Point(x + Width / 2, y + Height - 60)))
        {
            Left = x;
            Top = y;
            return;
        }
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width / 2 - 110;
        Top = work.Bottom - Height + 2;
    }

    public void ResetPosition()
    {
        _app.Store.Data.WidgetLeft = null;
        _app.Store.Data.WidgetTop = null;
        PlaceWindow();
    }

    public void ApplySettings()
    {
        Cat.AnimationsEnabled = Settings.Animations;
        if (!_reminding) Topmost = Settings.WidgetOnTop;
        ApplySize(keepBottom: true);
        UpdateGauge();
    }

    /// <summary>Small / Medium / Large cat; the widget grows upwards so it stays where you put it.</summary>
    private void ApplySize(bool keepBottom)
    {
        var size = Settings.CatSize switch { CatSize.Small => 92.0, CatSize.Large => 158.0, _ => 120.0 };
        Cat.Width = size;
        Cat.Height = size * 1.02;
        Body.Width = size + 116;

        var width = Math.Max(300, size + 140);
        var height = size * 1.02 + 230;
        if (keepBottom)
        {
            Left += (Width - width) / 2;
            Top += Height - height;
        }
        Width = width;
        Height = height;
    }

    private void CheckFullscreen()
    {
        var busy = Settings.RespectFullscreen && Native.IsUserBusy();
        if (busy == _hiddenForFullscreen) return;
        _hiddenForFullscreen = busy;
        if (busy) Hide();
        else Show();
    }

    // ------------------------------------------------------------------ gauge & hover

    private void UpdateGauge()
    {
        var h = _app.Hydration;
        AmountText.Text = h.GoalReached ? $"{h.TodayMl:N0} ml · goal done!" : $"{h.TodayMl:N0} / {h.GoalMl:N0} ml";
        var fraction = Math.Clamp(h.Fraction, 0, 1);
        if (Settings.Animations)
            ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fraction, TimeSpan.FromMilliseconds(700)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else
        {
            ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ProgressScale.ScaleX = fraction;
        }
        DrinkButton.ToolTip = $"I drank a glass (+{Settings.GlassMl} ml)";
        UndoButton.Visibility = h.Today.Entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Undo()
    {
        var removed = _app.Hydration.UndoLast();
        Say(removed != null ? $"Undone! Removed {removed.Ml} ml." : "Nothing to undo today~", 2.2);
        RestartNap();
    }

    // ------------------------------------------------------------------ right-click menu

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item($"I drank a glass (+{Settings.GlassMl} ml)", () =>
        {
            if (_reminding) Respond(ReminderResponse.Drank);
            else _app.RecordDrink(Settings.GlassMl);
        }));
        var undo = Item("Undo last drink", Undo);
        undo.IsEnabled = _app.Hydration.Today.Entries.Count > 0;
        menu.Items.Add(undo);
        menu.Items.Add(new Separator());

        var remindIn = new MenuItem { Header = "Remind me in" };
        foreach (var minutes in SnoozePicker.Choices)
        {
            remindIn.Items.Add(Item(SnoozePicker.Label(minutes), () =>
            {
                if (_reminding) Respond(ReminderResponse.Later, minutes);
                else
                {
                    _app.Scheduler.RemindIn(minutes);
                    Say($"Okay! I'll remind you in {SnoozePicker.Label(minutes)}~", 2.5);
                }
            }));
        }
        menu.Items.Add(remindIn);
        menu.Items.Add(Item("Remind me now", () =>
        {
            if (!_reminding) _app.Scheduler.TriggerNow();
        }));
        menu.Items.Add(new Separator());

        var size = new MenuItem { Header = "Cat size" };
        foreach (var (label, value) in new[] { ("Small", CatSize.Small), ("Medium", CatSize.Medium), ("Large", CatSize.Large) })
        {
            size.Items.Add(Item(label, () =>
            {
                Settings.CatSize = value;
                _app.Store.Save();
                ApplySize(keepBottom: true);
            }, Settings.CatSize == value));
        }
        menu.Items.Add(size);
        menu.Items.Add(Item("Keep on top of other windows", () =>
        {
            Settings.WidgetOnTop = !Settings.WidgetOnTop;
            _app.Store.Save();
            if (!_reminding) Topmost = Settings.WidgetOnTop;
        }, Settings.WidgetOnTop));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Open PawSip", _app.ShowMain));
        menu.Items.Add(Item("Hide the widget", () => _app.SetWidgetVisible(false)));

        menu.PlacementTarget = Body;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static MenuItem Item(string header, Action onClick, bool isChecked = false)
    {
        var item = new MenuItem { Header = header, IsChecked = isChecked };
        item.Click += (_, _) => onClick();
        return item;
    }

    private void ShowHoverButtons(bool show)
    {
        HoverButtons.IsHitTestVisible = show;
        HoverButtons.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(show ? 150 : 300)));
        if (show && Cat.Mood == CatMood.Sleep && !_reminding)
        {
            Cat.SetMood(CatMood.Idle); // wakes up when you come by
            RestartNap();
        }
    }

    private void RestartNap()
    {
        _napTimer.Stop();
        _napTimer.Start();
    }

    // ------------------------------------------------------------------ reminders

    public void Remind(string message)
    {
        _reminding = true;
        _reminderMessage = message;
        _bubbleTimer.Stop();
        _napTimer.Stop();
        Topmost = true; // make sure the reminder is seen even if the widget usually sits behind windows

        MessageText.Text = message;
        Actions.Visibility = Visibility.Visible;
        SnoozeHost.Visibility = Visibility.Collapsed;
        DismissButton.Visibility = Visibility.Visible;
        ShowBubble();

        Cat.SetMood(CatMood.Wave);
        Cat.Meow();
        Sounds.Play(Settings.Sound, Settings.Volume);
        After(2.6, () =>
        {
            if (_reminding && Cat.Mood == CatMood.Wave) Cat.SetMood(CatMood.Idle);
        });

        if (Settings.AutoHideMinutes > 0)
        {
            _reminderTimeout.Interval = TimeSpan.FromMinutes(Settings.AutoHideMinutes);
            _reminderTimeout.Start();
        }
    }

    private void Respond(ReminderResponse response, int snoozeMinutes = 0)
    {
        if (!_reminding) return;
        _reminding = false;
        _reminderTimeout.Stop();
        Topmost = Settings.WidgetOnTop;
        Actions.Visibility = Visibility.Collapsed;
        SnoozeHost.Visibility = Visibility.Collapsed;
        Responded?.Invoke(response, snoozeMinutes); // a "Drank" answer comes back through OnDrinkRecorded

        switch (response)
        {
            case ReminderResponse.Later:
                Say($"Okay! I'll be back in {SnoozePicker.Label(snoozeMinutes > 0 ? snoozeMinutes : Settings.SnoozeMinutes)}~", 2.5);
                Cat.SetMood(CatMood.Wave);
                After(2.0, () =>
                {
                    if (!_reminding) Cat.SetMood(CatMood.Idle);
                    RestartNap();
                });
                break;
            case ReminderResponse.Dismissed:
                Say("See you later! Stay hydrated~", 2.0);
                Cat.SetMood(CatMood.Idle);
                RestartNap();
                break;
            case ReminderResponse.Ignored:
                HideBubble();
                Cat.SetMood(CatMood.Sleep);
                break;
        }
    }

    private void OnDrinkRecorded(bool goalJustReached)
    {
        if (_reminding) return;
        _napTimer.Stop();
        Say($"{Messages.Thank(goalJustReached)} {_app.Hydration.TodayMl:N0} / {_app.Hydration.GoalMl:N0} ml", 3.5);
        Cat.SetMood(Settings.Animations ? CatMood.Drink : CatMood.Happy);
        if (!Settings.Animations) After(2.0, () => { Cat.SetMood(CatMood.Idle); RestartNap(); });
    }

    // ------------------------------------------------------------------ bubble

    private void Say(string message, double seconds)
    {
        MessageText.Text = message;
        Actions.Visibility = Visibility.Collapsed;
        DismissButton.Visibility = Visibility.Collapsed;
        ShowBubble();
        _bubbleTimer.Stop();
        _bubbleTimer.Interval = TimeSpan.FromSeconds(seconds);
        _bubbleTimer.Start();
    }

    private void ShowBubble()
    {
        var wasHidden = Bubble.Visibility != Visibility.Visible;
        Bubble.Visibility = Visibility.Visible;
        Bubble.BeginAnimation(OpacityProperty, null);
        Bubble.Opacity = 1;
        if (!wasHidden || !Settings.Animations) return;
        var pop = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(400)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55 } };
        BubbleScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        BubbleScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void HideBubble()
    {
        if (Bubble.Visibility != Visibility.Visible) return;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) =>
        {
            if (Bubble.Opacity <= 0.01) Bubble.Visibility = Visibility.Collapsed;
        };
        Bubble.BeginAnimation(OpacityProperty, fade);
    }

    // ------------------------------------------------------------------ drag & pet

    private void OnCatMouseDown(object sender, MouseButtonEventArgs e)
    {
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
            _app.Store.Data.WidgetLeft = Left;
            _app.Store.Data.WidgetTop = Top;
            _app.Store.Save();
            return;
        }

        // A click: pet the cat.
        if (Cat.Mood == CatMood.Sleep) Cat.SetMood(CatMood.Idle);
        Cat.Purr();
        if (!_reminding) Say(Messages.Pet(), 1.8);
        RestartNap();
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
