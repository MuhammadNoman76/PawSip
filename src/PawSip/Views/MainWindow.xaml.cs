using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PawSip.Interop;

namespace PawSip.Views;

public partial class MainWindow : Window
{
    private readonly App _app = App.Current;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };
    private DashboardPage? _dashboard;

    public MainWindow()
    {
        InitializeComponent();

        NavDashboard.Checked += (_, _) => Navigate(_dashboard ??= new DashboardPage(GoToStats));
        NavStats.Checked += (_, _) => Navigate(new StatsPage());
        NavSettings.Checked += (_, _) => Navigate(new SettingsPage());

        MinButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaxButton.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        CloseButton.Click += (_, _) => Close();
        PauseButton.Click += (_, _) =>
        {
            if (_app.Scheduler.IsPaused) _app.Scheduler.Resume();
            else _app.Scheduler.Pause(TimeSpan.FromHours(1));
        };

        StateChanged += (_, _) =>
        {
            // A maximized WindowChrome window overhangs the screen by the resize border.
            Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            if (_dashboard != null) _dashboard.Visibility = WindowState == WindowState.Minimized ? Visibility.Hidden : Visibility.Visible;
        };
        SourceInitialized += (_, _) => Native.UseRoundedCorners(new WindowInteropHelper(this).Handle);

        _app.Scheduler.ScheduleChanged += RefreshStatus;
        _app.Hydration.Changed += RefreshStatus;
        _clock.Tick += (_, _) => RefreshStatus();
        _clock.Start();
        Closed += (_, _) =>
        {
            _clock.Stop();
            _app.Scheduler.ScheduleChanged -= RefreshStatus;
            _app.Hydration.Changed -= RefreshStatus;
        };

        Navigate(_dashboard = new DashboardPage(GoToStats), animate: false);
        RefreshStatus();
    }

    private void GoToStats() => NavStats.IsChecked = true;

    /// <summary>Opens the dashboard (an update offer is shown there).</summary>
    public void ShowDashboard() => NavDashboard.IsChecked = true;

    private void Navigate(UserControl page, bool animate = true)
    {
        PageHost.Content = page;
        if (!animate || !_app.Store.Settings.Animations) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
        PageShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
    }

    private void RefreshStatus()
    {
        var s = _app.Scheduler;
        var settings = _app.Store.Settings;
        if (!settings.RemindersEnabled)
        {
            NextLabel.Text = "Reminders";
            NextTime.Text = "Turned off";
            PauseButton.Visibility = Visibility.Collapsed;
            return;
        }

        PauseButton.Visibility = Visibility.Visible;
        if (s.IsPaused)
        {
            NextLabel.Text = "Paused until";
            NextTime.Text = TrayIcon.FormatWhen(s.PausedUntil!.Value);
            PauseText.Text = "Resume";
            PauseIcon.Data = (Geometry)FindResource("Icon.Play");
        }
        else
        {
            NextLabel.Text = "Next reminder";
            NextTime.Text = s.NextReminderAt is { } at ? TrayIcon.FormatWhen(at) : "Soon";
            PauseText.Text = "Pause 1 hour";
            PauseIcon.Data = (Geometry)FindResource("Icon.Pause");
        }
    }
}
