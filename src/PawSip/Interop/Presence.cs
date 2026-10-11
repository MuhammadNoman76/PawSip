using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;
using PawSip.Core;

namespace PawSip.Interop;

/// <summary>
/// Knows whether you're actually at the PC: the screen is on, the session is unlocked and you've used the
/// keyboard or mouse recently. Reminders follow this instead of the clock.
/// </summary>
public static class Presence
{
    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private static Guid s_consoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

    private static HwndSource? s_window;
    private static IntPtr s_registration;

    /// <summary>Considered "at the PC" if there was input within this time.</summary>
    public static readonly TimeSpan ActiveWithin = TimeSpan.FromMinutes(5);

    public static bool DisplayOn { get; private set; } = true;
    public static bool Locked { get; private set; }

    /// <summary>Last time the user was seen at the PC (used to greet them when they come back).</summary>
    public static DateTime LastSeen { get; private set; } = DateTime.Now;

    public static bool IsUserActive
    {
        get
        {
            var active = DisplayOn && !Locked && Native.IdleTime < ActiveWithin;
            if (active) LastSeen = DateTime.Now;
            return active;
        }
    }

    public static void Start()
    {
        try
        {
            // A hidden (never shown) window that receives the display on/off notifications.
            s_window = new HwndSource(new HwndSourceParameters("PawSip presence") { WindowStyle = 0, Width = 0, Height = 0 });
            s_window.AddHook(WndProc);
            s_registration = RegisterPowerSettingNotification(s_window.Handle, ref s_consoleDisplayState, 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Presence");
        }
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public static void Stop()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        if (s_registration != IntPtr.Zero) UnregisterPowerSettingNotification(s_registration);
        s_window?.Dispose();
        s_window = null;
    }

    private static void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        Locked = e.Reason switch
        {
            SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect => true,
            SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect or SessionSwitchReason.SessionLogon => false,
            _ => Locked,
        };
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_POWERBROADCAST && wParam.ToInt32() == PBT_POWERSETTINGCHANGE && lParam != IntPtr.Zero)
        {
            var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
            if (setting.PowerSetting == s_consoleDisplayState)
                DisplayOn = setting.Data != 0; // 0 = off, 1 = on, 2 = dimmed
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
        public uint Data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid powerSetting, int flags);

    [DllImport("user32.dll")]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
}
