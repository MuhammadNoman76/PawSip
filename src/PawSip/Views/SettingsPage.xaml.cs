using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;
using PawSip.Controls;
using PawSip.Core;

namespace PawSip.Views;

public partial class SettingsPage : UserControl
{
    private readonly App _app = App.Current;
    private Stepper _goalStepper = null!;
    private Stepper _glassStepper = null!;
    private readonly List<RadioButton> _glassChips = new();
    private DateTime _clearArmedUntil;

    public SettingsPage()
    {
        InitializeComponent();
        // Keep the page where the user scrolled it (don't jump when something inside updates).
        var scroller = Content as ScrollViewer ?? ((Panel)Content).Children.OfType<ScrollViewer>().First();
        ((UIElement)scroller.Content).AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, e) => e.Handled = true));
        Build();
    }

    private Settings S => _app.Store.Settings;

    private void Build()
    {
        // ------------------------------------------------ Daily goal
        var goal = Card("Daily goal", "Icon.Drop", "WaterTint", "Water");
        goal.Children.Add(Row("Measure in", "Show your goal in glasses or milliliters.",
            Chips(new[] { ("Glasses", GoalUnit.Glasses), ("Milliliters", GoalUnit.Milliliters) }, S.Unit, v =>
            {
                S.Unit = v;
                ConfigureGoalStepper();
                Saved(timing: false);
            })));

        _goalStepper = new Stepper();
        _goalStepper.ValueChanged += v =>
        {
            S.GoalMl = S.Unit == GoalUnit.Glasses ? (int)v * S.GlassMl : (int)v;
            _app.Hydration.GoalChanged();
            Saved(timing: true);
        };
        ConfigureGoalStepper();
        goal.Children.Add(Row("Daily goal", "About 2 liters (8 glasses) is a good everyday target.", _goalStepper));

        var glassPanel = new StackPanel();
        var chips = new WrapPanel();
        foreach (var size in new[] { 150, 200, 250, 300, 350, 500 })
        {
            var chip = new RadioButton { Style = GetStyle("Chip"), Content = $"{size} ml", GroupName = "glass", Tag = size, IsChecked = S.GlassMl == size };
            chip.Checked += (_, _) => SetGlass((int)chip.Tag);
            _glassChips.Add(chip);
            chips.Children.Add(chip);
        }
        _glassStepper = new Stepper { Minimum = 50, Maximum = 1000, Step = 10, Format = v => $"{v:0} ml" };
        _glassStepper.Value = S.GlassMl;
        _glassStepper.ValueChanged += v => SetGlass((int)v);
        glassPanel.Children.Add(chips);
        glassPanel.Children.Add(_glassStepper);
        goal.Children.Add(Row("Glass size", "How much one glass holds.", glassPanel, last: true));

        // ------------------------------------------------ Reminders
        var rem = Card("Reminders", "Icon.Bell", "LavenderTint", "Lavender");
        rem.Children.Add(Row("Remind me to drink", "Your cat pops up when it's time for some water.",
            Switch(S.RemindersEnabled, v => { S.RemindersEnabled = v; Saved(timing: true); })));
        rem.Children.Add(Row("Remind every", null,
            Chips(new[] { 20, 30, 45, 60, 90, 120 }.Select(m => (m < 60 ? $"{m} min" : m % 60 == 0 ? $"{m / 60} h" : $"{m / 60}½ h", m)).ToArray(), S.IntervalMinutes, v =>
            {
                S.IntervalMinutes = v;
                Saved(timing: true);
            })));

        var hours = new StackPanel { Orientation = Orientation.Horizontal };
        var from = TimeStepper(S.ActiveStartMinutes, v => { S.ActiveStartMinutes = v; Saved(timing: true); });
        var to = TimeStepper(S.ActiveEndMinutes % (24 * 60), v => { S.ActiveEndMinutes = v; Saved(timing: true); });
        hours.Children.Add(from);
        hours.Children.Add(new TextBlock { Text = "to", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0), Foreground = Brush("Ink2") });
        hours.Children.Add(to);
        rem.Children.Add(Row("Active hours", "No reminders outside these hours — your cat sleeps too.", hours));

        rem.Children.Add(Row("Smart timing", "Remind a bit more often when you're behind, and relax when you're ahead.",
            Switch(S.SmartTiming, v => { S.SmartTiming = v; Saved(timing: true); })));
        rem.Children.Add(Row("Remind me later = ", "How long “Remind me later” waits.",
            Chips(new[] { ("5 min", 5), ("10 min", 10), ("15 min", 15), ("30 min", 30) }, S.SnoozeMinutes, v => { S.SnoozeMinutes = v; Saved(timing: false); })));
        rem.Children.Add(Row("Don't interrupt fullscreen", "Wait while a game, video or presentation is fullscreen.",
            Switch(S.RespectFullscreen, v => { S.RespectFullscreen = v; Saved(timing: false); })));
        rem.Children.Add(Row("Keep reminding after the goal", "Otherwise the cat rests once you've reached your goal.",
            Switch(S.RemindAfterGoal, v => { S.RemindAfterGoal = v; Saved(timing: true); }), last: true));

        // ------------------------------------------------ Companion
        var cat = Card("Cat companion", "Icon.Paw", "PeachTint", "Peach");
        cat.Children.Add(Row("Desktop cat widget", "A little cat that sits on your desktop with your water gauge. It reminds you right there.",
            Switch(S.ShowWidget, v => { S.ShowWidget = v; Saved(timing: false); })));
        cat.Children.Add(Row("Keep the widget on top", "Otherwise it stays behind your windows (it still pops up for reminders).",
            Switch(S.WidgetOnTop, v => { S.WidgetOnTop = v; Saved(timing: false); })));
        cat.Children.Add(Row("Reminder style", null,
            Chips(new[] { ("Cute cat", ReminderStyle.Cat), ("Windows notification", ReminderStyle.Notification) }, S.Style, v => { S.Style = v; Saved(timing: false); })));
        cat.Children.Add(Row("Animations", "Turn off for a calmer, still cat.",
            Switch(S.Animations, v => { S.Animations = v; Saved(timing: false); })));
        cat.Children.Add(Row("Cat size", null,
            Chips(new[] { ("Small", CatSize.Small), ("Medium", CatSize.Medium), ("Large", CatSize.Large) }, S.CatSize, v => { S.CatSize = v; Saved(timing: false); })));
        cat.Children.Add(Row("Cat leaves after", "If you don't answer, the cat naps and then walks away.",
            Chips(new[] { ("1 min", 1), ("3 min", 3), ("5 min", 5), ("Stays", 0) }, S.AutoHideMinutes, v => { S.AutoHideMinutes = v; Saved(timing: false); })));

        var catButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var preview = new Button { Style = GetStyle("PrimaryButton"), Height = 40, Padding = new Thickness(18, 0, 18, 0), FontSize = 13.5, Content = "Preview the cat" };
        preview.Click += (_, _) => _app.PreviewCat();
        var reset = new Button { Style = GetStyle("SoftButton"), Height = 40, Margin = new Thickness(10, 0, 0, 0), Content = "Reset cat position" };
        reset.Click += (_, _) =>
        {
            _app.ResetCatPositions();
            Saved(timing: false);
        };
        catButtons.Children.Add(preview);
        catButtons.Children.Add(reset);
        cat.Children.Add(Row("Try it", "Drag the cat anywhere — it remembers the spot.", catButtons, last: true));

        // ------------------------------------------------ Sound
        var sound = Card("Sound", "Icon.Bell", "PinkTint", "Pink");
        sound.Children.Add(Row("Reminder sound", null,
            Chips(new[] { ("Meow", ReminderSound.Meow), ("Chime", ReminderSound.Chime), ("Water drop", ReminderSound.Drop), ("None", ReminderSound.None) }, S.Sound, v =>
            {
                S.Sound = v;
                Sounds.Play(v, S.Volume);
                Saved(timing: false);
            })));
        var volume = new Slider { Style = GetStyle("SoftSlider"), Minimum = 0, Maximum = 100, Value = S.Volume, Width = 220, VerticalAlignment = VerticalAlignment.Center };
        volume.ValueChanged += (_, e) => S.Volume = (int)e.NewValue;
        volume.PreviewMouseUp += (_, _) =>
        {
            Sounds.Play(S.Sound == ReminderSound.None ? ReminderSound.Meow : S.Sound, S.Volume);
            Saved(timing: false);
        };
        sound.Children.Add(Row("Volume", null, volume, last: true));

        // ------------------------------------------------ System
        var sys = Card("System", "Icon.Settings", "MintTint", "Mint");
        sys.Children.Add(Row("Start with Windows", "PawSip starts quietly in the system tray when you sign in.",
            Switch(S.StartWithWindows, v =>
            {
                S.StartWithWindows = v;
                StartupManager.Apply(v);
                Saved(timing: false);
            })));
        var clear = new Button { Style = GetStyle("SoftButton"), Background = Brush("PinkTint"), Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x4F, 0x7F)), Content = "Clear history" };
        clear.Click += (_, _) =>
        {
            if (DateTime.Now > _clearArmedUntil)
            {
                _clearArmedUntil = DateTime.Now.AddSeconds(4);
                clear.Content = "Click again to delete everything";
                return;
            }
            _app.Hydration.ClearHistory();
            clear.Content = "History cleared";
            Saved(timing: true);
        };
        sys.Children.Add(Row("Your data", $"Stored only on this PC: {DataStore.Folder}", clear, last: true));
    }

    // ------------------------------------------------------------------ helpers

    private void ConfigureGoalStepper()
    {
        if (S.Unit == GoalUnit.Glasses)
        {
            _goalStepper.Minimum = 1;
            _goalStepper.Maximum = 24;
            _goalStepper.Step = 1;
            _goalStepper.Format = v => $"{v:0} {(v == 1 ? "glass" : "glasses")} · {v * S.GlassMl / 1000.0:0.##} L";
            _goalStepper.Value = S.GoalGlasses;
        }
        else
        {
            _goalStepper.Minimum = 500;
            _goalStepper.Maximum = 6000;
            _goalStepper.Step = 100;
            _goalStepper.Format = v => $"{v:N0} ml";
            _goalStepper.Value = Math.Round(S.GoalMl / 100.0) * 100;
        }
    }

    private void SetGlass(int ml)
    {
        if (ml == S.GlassMl) return;
        var glasses = S.GoalGlasses;
        S.GlassMl = ml;
        if (S.Unit == GoalUnit.Glasses)
        {
            S.GoalMl = glasses * ml; // keep the same number of glasses
            _app.Hydration.GoalChanged();
        }
        _glassStepper.Value = ml;
        foreach (var chip in _glassChips) chip.IsChecked = (int)chip.Tag == ml;
        ConfigureGoalStepper();
        Saved(timing: true);
    }

    private void Saved(bool timing)
    {
        _app.Store.Save();
        if (timing) _app.Scheduler.SettingsChanged();
        else _app.Hydration.GoalChanged(); // refreshes views that show settings
        _app.ApplyWidget();
        var show = new DoubleAnimationUsingKeyFrames();
        show.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        show.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300))));
        show.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700))));
        SavedPill.BeginAnimation(OpacityProperty, show);
    }

    private StackPanel Card(string title, string icon, string tint, string color)
    {
        var body = new StackPanel();
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(12), Background = Brush(tint), Margin = new Thickness(0, 0, 12, 0),
            Child = new Path { Data = (Geometry)FindResource(icon), Fill = Brush(color), Width = 17, Height = 17, Stretch = Stretch.Uniform },
        });
        header.Children.Add(new TextBlock { Text = title, Style = GetStyle("H2"), VerticalAlignment = VerticalAlignment.Center });
        body.Children.Add(header);
        Body.Children.Add(new Border { Style = GetStyle("Card"), Margin = new Thickness(0, 0, 0, 18), Child = body });
        return body;
    }

    private FrameworkElement Row(string title, string? caption, UIElement control, bool last = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) };
        text.Children.Add(new TextBlock { Text = title, Style = GetStyle("Label") });
        if (caption != null) text.Children.Add(new TextBlock { Text = caption, Style = GetStyle("Caption"), Margin = new Thickness(0, 3, 0, 0) });
        grid.Children.Add(text);

        if (control is FrameworkElement fe)
        {
            fe.VerticalAlignment = VerticalAlignment.Center;
            if (fe is CheckBox) fe.HorizontalAlignment = HorizontalAlignment.Right;
        }
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        var wrap = new StackPanel();
        wrap.Children.Add(grid);
        if (!last) wrap.Children.Add(new Rectangle { Height = 1, Fill = Brush("Line") });
        return wrap;
    }

    private WrapPanel Chips<T>((string Label, T Value)[] options, T current, Action<T> onChange)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 0, 0, -8) };
        var group = Guid.NewGuid().ToString();
        foreach (var (label, value) in options)
        {
            var chip = new RadioButton { Style = GetStyle("Chip"), Content = label, GroupName = group, IsChecked = EqualityComparer<T>.Default.Equals(value, current) };
            chip.Checked += (_, _) => onChange(value);
            panel.Children.Add(chip);
        }
        return panel;
    }

    private CheckBox Switch(bool value, Action<bool> onChange)
    {
        var box = new CheckBox { Style = GetStyle("Switch"), IsChecked = value };
        box.Checked += (_, _) => onChange(true);
        box.Unchecked += (_, _) => onChange(false);
        return box;
    }

    private static Stepper TimeStepper(int minutes, Action<int> onChange)
    {
        var stepper = new Stepper
        {
            Minimum = 0,
            Maximum = 24 * 60 - 30,
            Step = 30,
            Wrap = true,
            Format = v => DateTime.Today.AddMinutes(v).ToString("t", CultureInfo.CurrentCulture),
        };
        stepper.Value = Math.Round(minutes / 30.0) * 30 % (24 * 60);
        stepper.ValueChanged += v => onChange((int)v);
        return stepper;
    }

    private Style GetStyle(string key) => (Style)FindResource(key);
    private Brush Brush(string key) => (Brush)FindResource(key);
}
