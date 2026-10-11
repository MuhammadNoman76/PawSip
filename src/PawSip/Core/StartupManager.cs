using Microsoft.Win32;

namespace PawSip.Core;

/// <summary>"Start with Windows" via the per-user Run key (no admin rights needed).</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PawSip";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Compat.ExecutablePath;
                if (string.IsNullOrEmpty(exe)) return;
                var command = $"\"{exe}\" --background";
                if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
                    key.SetValue(ValueName, command);
            }
            else if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Startup");
        }
    }
}
