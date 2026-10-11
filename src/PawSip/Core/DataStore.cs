using System.Text.Json;
using System.Windows.Threading;

namespace PawSip.Core;

/// <summary>Loads and saves everything (settings, history, schedule) as JSON in %APPDATA%\PawSip.</summary>
public sealed class DataStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly DispatcherTimer _saveDelay;

    public DataStore()
    {
        Data = Load();
        _saveDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveDelay.Tick += (_, _) =>
        {
            _saveDelay.Stop();
            SaveNow();
        };
    }

    public AppData Data { get; }
    public Settings Settings => Data.Settings;

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PawSip");

    private static string FilePath => Path.Combine(Folder, "pawsip.json");

    private static AppData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath).Replace("\"Bell\"", "\"Meow\""); // the old "Bell" sound became "Meow"
                var data = JsonSerializer.Deserialize<AppData>(json, Json);
                if (data != null)
                {
                    data.Settings ??= new Settings();
                    data.Days ??= new Dictionary<string, DayRecord>();
                    Migrate(data);
                    Sanitize(data.Settings);
                    return data;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Load");
            // Keep the broken file for inspection instead of silently overwriting it.
            try { File.Copy(FilePath, FilePath + ".broken", overwrite: true); } catch { }
        }
        return new AppData();
    }

    public const int CurrentVersion = 2;

    /// <summary>One-time updates for data saved by earlier versions.</summary>
    private static void Migrate(AppData data)
    {
        if (data.Version < 2)
        {
            // 1.0.1: the meow and the small cat became the defaults.
            data.Settings.Sound = ReminderSound.Meow;
            data.Settings.CatSize = CatSize.Small;
            if (data.Settings.RepeatMinutes <= 0) data.Settings.RepeatMinutes = 15;
        }
        data.Version = CurrentVersion;
    }

    private static void Sanitize(Settings s)
    {
        s.RepeatMinutes = Math.Clamp(s.RepeatMinutes, 5, 120);
        s.GlassMl = Math.Clamp(s.GlassMl, 50, 1500);
        s.GoalMl = Math.Clamp(s.GoalMl, 250, 10000);
        s.IntervalMinutes = Math.Clamp(s.IntervalMinutes, 10, 240);
        s.SnoozeMinutes = Math.Clamp(s.SnoozeMinutes, 1, 120);
        s.ActiveStartMinutes = Math.Clamp(s.ActiveStartMinutes, 0, 24 * 60 - 1);
        s.ActiveEndMinutes = Math.Clamp(s.ActiveEndMinutes, 0, 24 * 60);
        s.Volume = Math.Clamp(s.Volume, 0, 100);
        s.AutoHideMinutes = Math.Clamp(s.AutoHideMinutes, 0, 30);
    }

    /// <summary>Saves shortly after the last change (several quick changes = one write).</summary>
    public void Save()
    {
        _saveDelay.Stop();
        _saveDelay.Start();
    }

    public void SaveNow()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Data, Json));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Save");
        }
    }
}
