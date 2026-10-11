using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;
using System.Windows.Threading;
using PawSip.Controls;
using PawSip.Core;

namespace PawSip.Views;

public partial class DashboardPage : UserControl
{
    private static readonly DependencyProperty ShownMlProperty = DependencyProperty.Register(
        nameof(ShownMl), typeof(double), typeof(DashboardPage),
        new PropertyMetadata(0.0, (d, e) => ((DashboardPage)d).AmountText.Text = ((double)e.NewValue).ToString("N0", CultureInfo.CurrentCulture)));

    private readonly App _app = App.Current;
    private readonly Action _openStats;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Random _rng = new();
    private DateTime _catMessageUntil;
    private bool _weekShown;

    public DashboardPage(Action openStats)
    {
        _openStats = openStats;
        InitializeComponent();
        // Keep the page where the user scrolled it (don't jump when something inside updates).
        var scroller = Content as ScrollViewer ?? ((Panel)Content).Children.OfType<ScrollViewer>().First();
        ((UIElement)scroller.Content).AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, e) => e.Handled = true));

        DrinkButton.Click += (_, _) => _app.RecordDrink(Settings.GlassMl);
        AddSmall.Click += (_, _) => _app.RecordDrink(100);
        AddLarge.Click += (_, _) => _app.RecordDrink(500);
        AddCustom.Click += (_, _) =>
        {
            CustomPanel.Visibility = CustomPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (CustomPanel.Visibility == Visibility.Visible)
            {
                CustomBox.Focus();
                CustomBox.SelectAll();
            }
        };
        CustomAdd.Click += (_, _) => AddCustomAmount();
        CustomBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) AddCustomAmount();
        };
        UndoButton.Click += (_, _) => _app.Hydration.UndoLast();
        StatsLink.Click += (_, _) => _openStats();

        Cat.MouseLeftButtonUp += (_, _) =>
        {
            Cat.Purr();
            SayForAWhile(Messages.Pet());
        };
        Cat.DrinkFinished += () =>
        {
            Cat.SetMood(CatMood.Happy);
            After(2.2, () => Cat.SetMood(CatMood.Idle));
        };

        _clock.Tick += (_, _) =>
        {
            RefreshNext();
            if (_app.Hydration.CurrentDay != DateTime.Today) _app.Hydration.CheckDayRollover();
        };

        Loaded += (_, _) =>
        {
            _app.Hydration.Changed += OnHydrationChanged;
            _app.Scheduler.ScheduleChanged += RefreshNext;
            _app.DrinkRecorded += OnDrinkRecorded;
            Cat.AnimationsEnabled = Settings.Animations;
            Ring.AnimationsEnabled = Settings.Animations;
            _clock.Start();
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            _app.Hydration.Changed -= OnHydrationChanged;
            _app.Scheduler.ScheduleChanged -= RefreshNext;
            _app.DrinkRecorded -= OnDrinkRecorded;
            _clock.Stop();
        };
    }

    private Settings Settings => _app.Store.Settings;

    private double ShownMl
    {
        get => (double)GetValue(ShownMlProperty);
        set => SetValue(ShownMlProperty, value);
    }

    private void OnHydrationChanged() => Refresh();

    private void AddCustomAmount()
    {
        if (int.TryParse(CustomBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var ml) && ml is >= 10 and <= 3000)
        {
            _app.RecordDrink(ml);
            CustomPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            SayForAWhile("Hmm, try an amount between 10 and 3000 ml~");
        }
    }

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        var h = _app.Hydration;
        var pace = _app.Scheduler.PaceDelta();

        Greeting.Text = Messages.Greeting() + "!";
        DateText.Text = DateTime.Now.ToString("dddd, MMM d", CultureInfo.CurrentCulture);
        SubGreeting.Text = h.GoalReached ? "You reached today's goal. Your cat is very proud!"
            : h.TodayMl == 0 ? "A fresh day — let's start with a glass of water."
            : "Let's keep you hydrated today.";

        Ring.AnimateTo(h.Fraction);
        if (Settings.Animations)
            BeginAnimation(ShownMlProperty, new DoubleAnimation(h.TodayMl, TimeSpan.FromMilliseconds(800)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else
        {
            BeginAnimation(ShownMlProperty, null);
            ShownMl = h.TodayMl;
        }
        GoalText.Text = $"of {h.GoalMl:N0} ml";
        PercentText.Text = $"{Math.Round(h.Fraction * 100):0}%";
        DrinkButtonText.Text = $"Drink a glass · {Settings.GlassMl} ml";

        if (h.GoalReached)
        {
            RemainingValue.Text = "Goal done!";
            RemainingCaption.Text = h.TodayMl > h.GoalMl ? $"{h.TodayMl - h.GoalMl:N0} ml extra" : "right on target";
        }
        else
        {
            RemainingValue.Text = $"{h.RemainingGlasses} {(h.RemainingGlasses == 1 ? "glass" : "glasses")}";
            RemainingCaption.Text = $"{h.RemainingMl:N0} ml to go";
        }

        var streak = h.Streak;
        StreakValue.Text = $"{streak} {(streak == 1 ? "day" : "days")}";
        (PaceValue.Text, PaceCaption.Text) = h.GoalReached ? ("Goal met!", "great job today")
            : pace < 0 ? ("A bit behind", "I'll remind you more often")
            : pace > 0 ? ("Ahead!", "reminders relaxed a little")
            : ("On track", "today's pace");

        if (DateTime.Now >= _catMessageUntil)
        {
            CatSays.Text = h.GoalReached ? "Goal reached! You're paw-some!"
                : h.TodayMl == 0 ? "Meow! Let's drink some water today."
                : pace < 0 ? "We're a little behind… one more glass?"
                : $"Purr~ {h.RemainingGlasses} more {(h.RemainingGlasses == 1 ? "glass" : "glasses")} to go!";
        }

        BuildCups();
        BuildSips();
        BuildWeek();
        RefreshNext();
    }

    private void RefreshNext()
    {
        var s = _app.Scheduler;
        if (!Settings.RemindersEnabled)
        {
            NextValue.Text = "Off";
            NextCaption.Text = "reminders are turned off";
        }
        else if (s.IsPaused)
        {
            NextValue.Text = "Paused";
            NextCaption.Text = "until " + TrayIcon.FormatWhen(s.PausedUntil!.Value);
        }
        else if (s.NextReminderAt is { } at)
        {
            NextValue.Text = at.ToString("t", CultureInfo.CurrentCulture);
            var left = at - DateTime.Now;
            NextCaption.Text = at.Date > DateTime.Today ? "tomorrow"
                : left.TotalMinutes < 1 ? "any moment now"
                : left.TotalMinutes < 60 ? $"in {Math.Ceiling(left.TotalMinutes):0} min"
                : $"in {(int)left.TotalHours} h {left.Minutes} min";
        }
        else
        {
            NextValue.Text = "Soon";
            NextCaption.Text = "next reminder";
        }
    }

    private void BuildCups()
    {
        CupRow.Children.Clear();
        var goalGlasses = Math.Min(Settings.GoalGlasses, 16);
        var full = _app.Hydration.GlassesToday;
        var glass = (Geometry)FindResource("Icon.Glass");
        for (var i = 0; i < goalGlasses; i++)
        {
            var filled = full >= i + 1 - 0.001;
            CupRow.Children.Add(new Path
            {
                Data = filled ? (Geometry)FindResource("Icon.Drop") : glass,
                Fill = filled ? (Brush)FindResource("WaterGradient") : (Brush)FindResource("Line"),
                Width = 18,
                Height = 20,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 7, 6),
            });
        }
        GlassesLine.Text = $"{_app.Hydration.GlassesToday:0.#} of {Settings.GoalGlasses} glasses so far";
    }

    private void BuildSips()
    {
        SipList.Children.Clear();
        var entries = _app.Hydration.Today.Entries.AsEnumerable().Reverse().Take(6).ToList();
        EmptySips.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var entry in entries)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(11), Background = (Brush)FindResource("WaterTint"),
                Child = new Path { Data = (Geometry)FindResource("Icon.Drop"), Fill = (Brush)FindResource("Water"), Width = 13, Height = 13, Stretch = Stretch.Uniform },
            };
            row.Children.Add(badge);

            var amount = new TextBlock { Text = $"{entry.Ml} ml", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(amount, 1);
            row.Children.Add(amount);

            var time = new TextBlock { Text = entry.At.ToString("t", CultureInfo.CurrentCulture), Foreground = (Brush)FindResource("Ink3"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            Grid.SetColumn(time, 2);
            row.Children.Add(time);

            var remove = new Button
            {
                Style = (Style)FindResource("IconButton"), Width = 28, Height = 28, ToolTip = "Remove",
                Content = new Path { Data = (Geometry)FindResource("Icon.Close"), Style = (Style)FindResource("Icon"), Width = 10, Height = 10, Fill = (Brush)FindResource("Ink3") },
            };
            var captured = entry;
            remove.Click += (_, _) => _app.Hydration.RemoveEntry(captured);
            Grid.SetColumn(remove, 3);
            row.Children.Add(remove);
            SipList.Children.Add(row);
        }
    }

    private void BuildWeek()
    {
        var days = _app.Hydration.Range(DateTime.Today.AddDays(-6), DateTime.Today);
        var items = days.Select(d => new BarItem(
            d.Date == DateTime.Today ? "Today" : d.Date.ToString("ddd", CultureInfo.CurrentCulture),
            $"{d.Date:ddd d} · {d.Ml:N0} ml",
            d.Ml, d.GoalMl > 0 ? d.GoalMl : Settings.GoalMl, d.Date == DateTime.Today)).ToList();
        WeekChart.SetItems(items, animate: Settings.Animations && !_weekShown);
        _weekShown = true;
        var tracked = days.Where(d => d.Ml > 0).ToList();
        WeekCaption.Text = tracked.Count == 0 ? "Your week starts today" : $"Average {tracked.Average(d => d.Ml):N0} ml on tracked days";
    }

    // ------------------------------------------------------------------ celebrations

    private void OnDrinkRecorded(bool goalJustReached)
    {
        if (!Settings.Animations)
        {
            if (goalJustReached) SayForAWhile("Goal reached! You're paw-some!");
            return;
        }

        RippleScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 4.2, TimeSpan.FromMilliseconds(650)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        RippleScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 4.2, TimeSpan.FromMilliseconds(650)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        Ripple.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 0, TimeSpan.FromMilliseconds(650)));

        Cat.SetMood(CatMood.Drink);
        SayForAWhile(goalJustReached ? "Goal reached! You're paw-some!" : Messages.Thank(false));
        if (goalJustReached) Celebrate();
    }

    private void Celebrate()
    {
        var colors = new[] { "#6CC5FF", "#7C8CFF", "#FF8DB3", "#5CCFA9", "#FFB27D", "#A497F7" };
        var origin = new Point(118 + 30, 118 + 26);
        for (var i = 0; i < 26; i++)
        {
            var heart = i % 3 == 0;
            Shape piece = heart
                ? new Path { Data = (Geometry)FindResource("Icon.Sparkle"), Width = 14, Height = 14, Stretch = Stretch.Uniform }
                : new Ellipse { Width = 9, Height = 9 };
            piece.Fill = (Brush)new BrushConverter().ConvertFromString(colors[_rng.Next(colors.Length)])!;
            Canvas.SetLeft(piece, origin.X);
            Canvas.SetTop(piece, origin.Y);
            var move = new TranslateTransform();
            piece.RenderTransform = move;
            Confetti.Children.Add(piece);

            var angle = _rng.NextDouble() * Math.PI * 2;
            var distance = 90 + _rng.NextDouble() * 120;
            var duration = TimeSpan.FromMilliseconds(900 + _rng.Next(500));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(Math.Cos(angle) * distance, duration) { EasingFunction = ease });
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(Math.Sin(angle) * distance + 40, duration) { EasingFunction = ease });
            var fade = new DoubleAnimation(1, 0, duration) { BeginTime = TimeSpan.FromMilliseconds(200) };
            fade.Completed += (_, _) => Confetti.Children.Remove(piece);
            piece.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void SayForAWhile(string text)
    {
        CatSays.Text = text;
        _catMessageUntil = DateTime.Now.AddSeconds(4);
        After(4.1, () =>
        {
            if (DateTime.Now >= _catMessageUntil) Refresh();
        });
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
