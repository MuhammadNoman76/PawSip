using System.Text.Json.Serialization;

namespace PawSip.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GoalUnit { Glasses, Milliliters }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReminderStyle { Cat, Notification }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatSize { Small, Medium, Large }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReminderSound { None, Meow, Chime, Drop }

public sealed class Settings
{
    // Goal
    public GoalUnit Unit { get; set; } = GoalUnit.Glasses;
    public int GoalMl { get; set; } = 2000;
    public int GlassMl { get; set; } = 250;

    // Reminders
    public bool RemindersEnabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 45;
    /// <summary>Remind any time you're actually at the PC (screen on + recent keyboard/mouse), ignoring the clock.</summary>
    public bool FollowActivity { get; set; } = true;

    public int ActiveStartMinutes { get; set; } = 8 * 60;
    public int ActiveEndMinutes { get; set; } = 22 * 60;
    public bool SmartTiming { get; set; } = true;
    public int SnoozeMinutes { get; set; } = 10;

    /// <summary>If a reminder is dismissed or ignored (no drink), the cat comes back after this many minutes.</summary>
    public int RepeatMinutes { get; set; } = 15;
    public bool RespectFullscreen { get; set; } = true;
    public bool RemindAfterGoal { get; set; }

    // Companion
    public ReminderStyle Style { get; set; } = ReminderStyle.Cat;
    public bool Animations { get; set; } = true;
    public CatSize CatSize { get; set; } = CatSize.Small;
    public int AutoHideMinutes { get; set; } = 3;
    public bool ShowWidget { get; set; } = true;
    public bool WidgetOnTop { get; set; } = true;

    // Sound
    public ReminderSound Sound { get; set; } = ReminderSound.Meow;
    public int Volume { get; set; } = 60;

    // System
    public bool StartWithWindows { get; set; } = true;

    public int GoalGlasses => Math.Max(1, (int)Math.Round(GoalMl / (double)Math.Max(1, GlassMl)));
}

public sealed class DrinkEntry
{
    public DateTime At { get; set; }
    public int Ml { get; set; }
}

public sealed class DayRecord
{
    public int GoalMl { get; set; }
    public List<DrinkEntry> Entries { get; set; } = new();

    [JsonIgnore]
    public int TotalMl => Entries.Sum(e => e.Ml);

    [JsonIgnore]
    public bool GoalMet => GoalMl > 0 && TotalMl >= GoalMl;
}

public sealed class AppData
{
    /// <summary>Data format version (see DataStore.Migrate).</summary>
    public int Version { get; set; } = DataStore.CurrentVersion;
    public Settings Settings { get; set; } = new();

    /// <summary>History, one record per day ("yyyy-MM-dd").</summary>
    public Dictionary<string, DayRecord> Days { get; set; } = new();

    public DateTime? NextReminderAt { get; set; }
    public DateTime? PausedUntil { get; set; }
    public double? CatLeft { get; set; }
    public double? CatTop { get; set; }
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }
    public bool TrayHintShown { get; set; }
    public bool StartupConfigured { get; set; }
}
