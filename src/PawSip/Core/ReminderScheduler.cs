using System.Windows.Threading;
using PawSip.Interop;

namespace PawSip.Core;

public enum ReminderResponse { Drank, Later, Dismissed, Ignored }

/// <summary>What kind of reminder this is (changes what the cat says).</summary>
public enum ReminderKind { Regular, FirstOfDay, WelcomeBack }

/// <summary>
/// Decides when the cat shows up. One cheap check every 20 seconds (no busy loops), schedule stored on
/// disk so it survives restarts, quiet outside active hours, during fullscreen apps and while you're away.
/// </summary>
public sealed class ReminderScheduler
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(20);
    private const int MinIntervalMinutes = 15;

    private readonly DataStore _store;
    private readonly Hydration _hydration;
    private readonly DispatcherTimer _timer;
    private bool _reminderOpen;

    public ReminderScheduler(DataStore store, Hydration hydration)
    {
        _store = store;
        _hydration = hydration;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = CheckEvery };
        _timer.Tick += (_, _) => Check();
    }

    /// <summary>Time to remind - show the cat (or a notification) and call <see cref="Respond"/> afterwards.</summary>
    public event Action<ReminderKind>? ReminderDue;

    /// <summary>Next reminder time, pause state or day changed.</summary>
    public event Action? ScheduleChanged;

    private Settings Settings => _store.Settings;

    public DateTime? NextReminderAt => _store.Data.NextReminderAt;
    public DateTime? PausedUntil => _store.Data.PausedUntil is { } p && p > DateTime.Now ? p : null;
    public bool IsPaused => PausedUntil != null;
    private DateTime _lastFired = DateTime.MinValue;

    public void Start()
    {
        var next = _store.Data.NextReminderAt;
        var now = DateTime.Now;
        // Missed while the PC was off/asleep: don't jump out right at log-in, give it a couple of minutes.
        if (next == null || next < now) Schedule(ClampToActive(now.AddMinutes(next == null ? Settings.IntervalMinutes : 2)));
        _timer.Start();
        Check();
    }

    public void Stop() => _timer.Stop();

    private void Check()
    {
        _hydration.CheckDayRollover();
        if (_reminderOpen || !Settings.RemindersEnabled || IsPaused) return;

        if (_store.Data.PausedUntil != null)
        {
            // pause just ended
            _store.Data.PausedUntil = null;
            Schedule(ClampToActive(DateTime.Now.AddMinutes(1)));
        }

        var due = _store.Data.NextReminderAt;
        if (due == null)
        {
            Reschedule();
            return;
        }

        var now = DateTime.Now;
        var present = Presence.IsUserActive; // screen on, unlocked, keyboard/mouse used in the last few minutes
        if (now < due) return;

        if (!Settings.FollowActivity && !IsActive(now))
        {
            Schedule(ClampToActive(now));
            return;
        }
        if (!present) return; // away, screen off or locked: the cat waits and greets you when you're back
        if (Settings.RespectFullscreen && Native.IsUserBusy()) return; // try again on the next check

        var kind = _lastFired.Date != DateTime.Today ? ReminderKind.FirstOfDay
            : (now - due.Value) > TimeSpan.FromMinutes(20) ? ReminderKind.WelcomeBack
            : ReminderKind.Regular;
        _lastFired = now;
        _reminderOpen = true;
        ReminderDue?.Invoke(kind);
    }

    /// <summary>Show a reminder right now (tray "Remind me now").</summary>
    public void TriggerNow()
    {
        if (_reminderOpen) return;
        _reminderOpen = true;
        ReminderDue?.Invoke(ReminderKind.Regular);
    }

    /// <summary>How many reminders in a row ended without a drink (the cat gets a little more insistent).</summary>
    public int MissedInARow { get; private set; }

    /// <param name="snoozeMinutes">For "Remind me later": how long to wait (0 = the snooze setting).</param>
    public void Respond(ReminderResponse response, int snoozeMinutes = 0)
    {
        _reminderOpen = false;
        switch (response)
        {
            case ReminderResponse.Drank:
                MissedInARow = 0;
                Reschedule();
                break;
            case ReminderResponse.Later:
                // The user picked a time - respect it.
                MissedInARow++;
                Schedule(ClampToActive(DateTime.Now.AddMinutes(snoozeMinutes > 0 ? snoozeMinutes : Settings.SnoozeMinutes)));
                break;
            default:
                // Dismissed or ignored without drinking: come back soon, again and again, until there's a drink.
                MissedInARow++;
                Schedule(ClampToActive(DateTime.Now.AddMinutes(Settings.RepeatMinutes)));
                break;
        }
    }

    /// <summary>A drink was logged somewhere (dashboard, widget, tray): back to the normal rhythm.</summary>
    public void DrinkLogged()
    {
        MissedInARow = 0;
        Reschedule();
    }

    /// <summary>"Remind me in 30 minutes" - works any time, also ends a pause.</summary>
    public void RemindIn(int minutes)
    {
        _store.Data.PausedUntil = null;
        Schedule(DateTime.Now.AddMinutes(Math.Max(1, minutes)));
    }

    /// <summary>Plan the next reminder from now (after a drink, a dismiss or a settings change).</summary>
    public void Reschedule()
    {
        var now = DateTime.Now;
        if (_hydration.GoalReached && !Settings.RemindAfterGoal)
        {
            // Goal done - the cat rests until tomorrow (morning, or as soon as you're at the PC after midnight).
            var tomorrow = DateTime.Today.AddDays(1);
            Schedule(Settings.FollowActivity
                ? tomorrow.AddMinutes(Math.Min(Settings.IntervalMinutes, 60))
                : ClampToActive(tomorrow.AddMinutes(Settings.ActiveStartMinutes)));
            return;
        }
        Schedule(ClampToActive(now.AddMinutes(CurrentIntervalMinutes())));
    }

    /// <summary>The configured interval, nudged by how today is going (smart timing).</summary>
    public double CurrentIntervalMinutes()
    {
        double minutes = Settings.IntervalMinutes;
        if (!Settings.SmartTiming) return minutes;

        var diff = ExpectedFraction(DateTime.Now) - _hydration.Fraction;
        var factor = diff switch
        {
            > 0.25 => 0.65,  // well behind: remind more often
            > 0.10 => 0.8,
            < -0.25 => 1.5,  // well ahead: relax
            < -0.10 => 1.25,
            _ => 1.0,
        };
        return Math.Max(Math.Min(MinIntervalMinutes, Settings.IntervalMinutes), minutes * factor);
    }

    /// <summary>How much of the goal "should" be done by now, spread evenly over the active hours.</summary>
    public double ExpectedFraction(DateTime at)
    {
        var start = Settings.ActiveStartMinutes;
        var end = Settings.ActiveEndMinutes;
        var length = end > start ? end - start : 24 * 60 - start + end;
        if (length <= 0) length = 24 * 60;

        var tod = (int)at.TimeOfDay.TotalMinutes;
        var elapsed = tod >= start ? tod - start : tod + 24 * 60 - start;
        if (!IsActive(at)) return tod < start && end > start ? 0 : 1;
        return MathEx.Clamp(elapsed / (double)length, 0, 1);
    }

    /// <summary>"on track" / "a little behind" / "ahead" for the dashboard.</summary>
    public int PaceDelta()
    {
        var diff = _hydration.Fraction - ExpectedFraction(DateTime.Now);
        return diff switch { < -0.1 => -1, > 0.1 => 1, _ => 0 };
    }

    public bool IsActive(DateTime at)
    {
        var start = Settings.ActiveStartMinutes;
        var end = Settings.ActiveEndMinutes;
        if (start == end) return true;
        var tod = (int)at.TimeOfDay.TotalMinutes;
        return start < end ? tod >= start && tod < end : tod >= start || tod < end;
    }

    /// <summary>Moves a time into the active hours - unless reminders follow your activity instead of the clock.</summary>
    private DateTime ClampToActive(DateTime at)
    {
        if (Settings.FollowActivity || IsActive(at)) return at;
        var startToday = at.Date.AddMinutes(Settings.ActiveStartMinutes);
        return startToday > at ? startToday : startToday.AddDays(1);
    }

    private void Schedule(DateTime at)
    {
        _store.Data.NextReminderAt = at;
        _store.Save();
        ScheduleChanged?.Invoke();
    }

    public void Pause(TimeSpan? duration)
    {
        var until = duration is { } d ? DateTime.Now + d : DateTime.Today.AddDays(1).AddMinutes(Settings.ActiveStartMinutes);
        _store.Data.PausedUntil = until;
        _store.Data.NextReminderAt = until;
        _store.Save();
        ScheduleChanged?.Invoke();
    }

    public void Resume()
    {
        _store.Data.PausedUntil = null;
        Reschedule();
    }

    /// <summary>Settings that affect timing changed.</summary>
    public void SettingsChanged()
    {
        if (!IsPaused) Reschedule();
        ScheduleChanged?.Invoke();
    }
}
