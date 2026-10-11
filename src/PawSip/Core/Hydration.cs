using System.Globalization;

namespace PawSip.Core;

public readonly record struct DayStat(DateTime Date, int Ml, int GoalMl)
{
    public double Fraction => GoalMl <= 0 ? 0 : Ml / (double)GoalMl;
    public bool GoalMet => GoalMl > 0 && Ml >= GoalMl;
}

/// <summary>Today's intake and the hydration history. A new day starts automatically at midnight.</summary>
public sealed class Hydration
{
    private readonly DataStore _store;

    public Hydration(DataStore store)
    {
        _store = store;
        CurrentDay = DateTime.Today;
    }

    /// <summary>Raised whenever intake, goal or the day changes.</summary>
    public event Action? Changed;

    public DateTime CurrentDay { get; private set; }
    private Settings Settings => _store.Settings;

    public static string Key(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public DayRecord Today => GetOrCreate(DateTime.Today);
    public int TodayMl => Today.TotalMl;
    public int GoalMl => Settings.GoalMl;
    public int GlassMl => Settings.GlassMl;
    public double Fraction => GoalMl <= 0 ? 0 : TodayMl / (double)GoalMl;
    public bool GoalReached => TodayMl >= GoalMl;
    public int RemainingMl => Math.Max(0, GoalMl - TodayMl);
    public int RemainingGlasses => (int)Math.Ceiling(RemainingMl / (double)Math.Max(1, GlassMl));
    public double GlassesToday => TodayMl / (double)Math.Max(1, GlassMl);

    private DayRecord GetOrCreate(DateTime day)
    {
        var key = Key(day);
        if (!_store.Data.Days.TryGetValue(key, out var record))
        {
            record = new DayRecord { GoalMl = Settings.GoalMl };
            _store.Data.Days[key] = record;
        }
        return record;
    }

    /// <summary>Call regularly: starts a fresh day after midnight (history is kept).</summary>
    public bool CheckDayRollover()
    {
        if (DateTime.Today == CurrentDay) return false;
        CurrentDay = DateTime.Today;
        _ = Today; // creates today's record with the current goal
        _store.Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Returns true if this drink completed the daily goal.</summary>
    public bool AddDrink(int ml)
    {
        if (ml <= 0) return false;
        var wasReached = GoalReached;
        Today.Entries.Add(new DrinkEntry { At = DateTime.Now, Ml = ml });
        _store.Save();
        Changed?.Invoke();
        return !wasReached && GoalReached;
    }

    public void RemoveEntry(DrinkEntry entry)
    {
        if (Today.Entries.Remove(entry))
        {
            _store.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Removes today's most recent drink; returns it (or null if there was none).</summary>
    public DrinkEntry? UndoLast()
    {
        var last = Today.Entries.LastOrDefault();
        if (last != null) RemoveEntry(last);
        return last;
    }

    /// <summary>The goal changed in settings: today's record follows; past days keep their goal.</summary>
    public void GoalChanged()
    {
        Today.GoalMl = Settings.GoalMl;
        _store.Save();
        Changed?.Invoke();
    }

    public DayStat GetDay(DateTime day)
    {
        if (_store.Data.Days.TryGetValue(Key(day), out var r)) return new DayStat(day.Date, r.TotalMl, r.GoalMl);
        return new DayStat(day.Date, 0, day.Date == DateTime.Today ? Settings.GoalMl : 0);
    }

    public IReadOnlyList<DayStat> Range(DateTime from, DateTime to)
    {
        var list = new List<DayStat>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1)) list.Add(GetDay(d));
        return list;
    }

    /// <summary>Consecutive days the goal was met (today counts once it's met).</summary>
    public int Streak
    {
        get
        {
            var streak = 0;
            var day = GetDay(DateTime.Today).GoalMet ? DateTime.Today : DateTime.Today.AddDays(-1);
            while (GetDay(day).GoalMet)
            {
                streak++;
                day = day.AddDays(-1);
            }
            return streak;
        }
    }

    public void ClearHistory()
    {
        _store.Data.Days.Clear();
        _ = Today;
        _store.Save();
        Changed?.Invoke();
    }
}
