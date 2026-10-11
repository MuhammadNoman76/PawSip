using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PawSip.Core;
using PawSip.Interop;
using PawSip.Views;

namespace PawSip;

public partial class App : Application
{
    private MainWindow? _main;
    private CatReminderWindow? _cat;
    private CatWidgetWindow? _widget;
    private TrayIcon? _tray;

    public DataStore Store { get; private set; } = null!;
    public Hydration Hydration { get; private set; } = null!;
    public ReminderScheduler Scheduler { get; private set; } = null!;

    public static new App Current => (App)Application.Current;

    /// <summary>A drink was logged anywhere (dashboard, cat, tray). Bool = the goal was just reached.</summary>
    public event Action<bool>? DrinkRecorded;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "UI");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error(args.ExceptionObject as Exception, "AppDomain");

        // Used by the installer: "--set-startup on|off" stores the choice from setup, then exits.
        var startupArg = Array.FindIndex(e.Args, a => string.Equals(a, "--set-startup", StringComparison.OrdinalIgnoreCase));
        if (startupArg >= 0 && startupArg + 1 < e.Args.Length)
        {
            var store = new DataStore();
            store.Settings.StartWithWindows = string.Equals(e.Args[startupArg + 1], "on", StringComparison.OrdinalIgnoreCase);
            store.Data.StartupConfigured = true;
            store.SaveNow();
            StartupManager.Apply(store.Settings.StartWithWindows);
            Shutdown();
            return;
        }

        var background = e.Args.Any(a => string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase));
        if (!SingleInstance.TryStart())
        {
            // Already running: just bring up its window (unless Windows is starting us again at log-in).
            if (!background) SingleInstance.SignalFirstInstance();
            Shutdown();
            return;
        }

        Store = new DataStore();
        Hydration = new Hydration(Store);
        Scheduler = new ReminderScheduler(Store, Hydration);

        if (!Store.Data.StartupConfigured)
        {
            Store.Data.StartupConfigured = true;
            Store.Save();
        }
        StartupManager.Apply(Store.Settings.StartWithWindows);

        _tray = new TrayIcon(this);
        _tray.Show();
        Hydration.Changed += () => _tray?.Refresh();
        Scheduler.ScheduleChanged += () => _tray?.Refresh();
        Scheduler.ReminderDue += OnReminderDue;
        Scheduler.Start();
        ApplyWidget();

        SingleInstance.ListenForShow(() => Dispatcher.BeginInvoke(ShowMain));
        SystemEvents.SessionEnding += (_, _) => Store.SaveNow();

        if (background) TrimSoon();
        else ShowMain();
    }

    // ------------------------------------------------------------------ main window

    public void ShowMain()
    {
        if (_main == null)
        {
            _main = new MainWindow();
            _main.Closed += (_, _) =>
            {
                _main = null;
                if (!Store.Data.TrayHintShown)
                {
                    Store.Data.TrayHintShown = true;
                    Store.Save();
                    _tray?.ShowBalloon("PawSip is still here", "I'll keep reminding you from the system tray. Meow!");
                }
                TrimSoon();
            };
            _main.Show();
        }
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    // ------------------------------------------------------------------ drinking & reminders

    public void RecordDrink(int ml, bool fromTray = false)
    {
        var goalJustReached = Hydration.AddDrink(ml);
        Scheduler.Reschedule();
        DrinkRecorded?.Invoke(goalJustReached);
        if (fromTray)
        {
            _tray?.ShowBalloon(goalJustReached ? "Goal reached! You're paw-some!" : $"+{ml} ml — purr-fect!",
                $"{Hydration.TodayMl:N0} / {Hydration.GoalMl:N0} ml today");
        }
    }

    private void OnReminderDue(bool firstOfDay)
    {
        var message = Messages.Reminder(firstOfDay, Scheduler.PaceDelta());
        if (Store.Settings.Style == ReminderStyle.Notification)
        {
            Sounds.Play(Store.Settings.Sound, Store.Settings.Volume);
            _tray?.ShowBalloon(message + " 💧", $"{Hydration.TodayMl:N0} / {Hydration.GoalMl:N0} ml · right-click the tray icon to log a glass");
            Scheduler.Respond(ReminderResponse.Dismissed);
            return;
        }

        // The desktop widget cat reminds you right where it sits; otherwise a cat walks in.
        if (_widget?.IsAvailable == true) _widget.Remind(message);
        else ShowCat(message, preview: false);
    }

    private void HandleResponse(ReminderResponse response, int snoozeMinutes)
    {
        if (response == ReminderResponse.Drank)
        {
            var goalJustReached = Hydration.AddDrink(Store.Settings.GlassMl);
            DrinkRecorded?.Invoke(goalJustReached);
        }
        Scheduler.Respond(response, snoozeMinutes);
    }

    // ------------------------------------------------------------------ desktop widget

    public bool WidgetVisible => _widget != null;

    /// <summary>Creates, updates or removes the desktop widget to match the settings.</summary>
    public void ApplyWidget()
    {
        if (Store.Settings.ShowWidget)
        {
            if (_widget == null)
            {
                _widget = new CatWidgetWindow(this);
                _widget.Responded += HandleResponse;
                _widget.Closed += (_, _) => _widget = null;
                _widget.Show();
            }
            else
            {
                _widget.ApplySettings();
            }
        }
        else if (_widget != null)
        {
            _widget.Close();
            _widget = null;
            TrimSoon();
        }
        _tray?.Refresh();
    }

    public void SetWidgetVisible(bool visible)
    {
        Store.Settings.ShowWidget = visible;
        Store.Save();
        ApplyWidget();
        if (!visible) _tray?.ShowBalloon("Desktop cat hidden", "Bring it back any time from the tray icon or Settings.");
    }

    public void ResetCatPositions()
    {
        Store.Data.CatLeft = null;
        Store.Data.CatTop = null;
        _widget?.ResetPosition();
        Store.Save();
    }

    /// <summary>Settings → "Preview the cat": same visit, but nothing is logged or rescheduled.</summary>
    public void PreviewCat() => ShowCat("Meow! This is how I'll remind you to drink water!", preview: true);

    private void ShowCat(string message, bool preview)
    {
        if (_cat != null)
        {
            if (!preview) Scheduler.Respond(ReminderResponse.Ignored);
            return;
        }

        _cat = new CatReminderWindow(Store, Hydration, message);
        _cat.Responded += (response, snoozeMinutes) =>
        {
            if (!preview) HandleResponse(response, snoozeMinutes);
        };
        _cat.Closed += (_, _) =>
        {
            _cat = null;
            TrimSoon();
        };
        _cat.Show();
    }

    // ------------------------------------------------------------------ lifecycle

    public void ExitApp()
    {
        Scheduler.Stop();
        Store.SaveNow();
        _cat?.Close();
        _widget?.Close();
        _main?.Close();
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Store?.SaveNow();
        _tray?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Hand unused memory back to Windows once nothing is on screen.</summary>
    private void TrimSoon()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_main == null && _cat == null) Native.TrimMemory();
        };
        timer.Start();
    }
}
