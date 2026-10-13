using System.Drawing;
using System.Windows;
using PawSip.Core;
using Forms = System.Windows.Forms;

namespace PawSip.Views;

/// <summary>The system tray icon and its menu - PawSip lives here when the window is closed.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly App _app;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _status = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem _drink = new();
    private readonly Forms.ToolStripMenuItem _pause = new("Pause reminders");
    private readonly Forms.ToolStripMenuItem _resume = new("Resume reminders");
    private readonly Forms.ToolStripMenuItem _widget = new("Show desktop cat");
    private readonly Forms.ToolStripMenuItem _update = new() { Visible = false };
    private readonly Forms.ToolStripMenuItem _checkUpdates = new("Check for updates…");

    public TrayIcon(App app)
    {
        _app = app;

        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new Forms.ToolStripProfessionalRenderer(new PastelColors()) { RoundedEdges = true },
            Font = new Font("Segoe UI", 9.75f),
            ShowImageMargin = false,
            Padding = new Forms.Padding(4),
        };

        var open = new Forms.ToolStripMenuItem("Open PawSip") { Font = new Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Bold) };
        open.Click += (_, _) => _app.ShowMain();
        _drink.Click += (_, _) => _app.RecordDrink(_app.Store.Settings.GlassMl, fromTray: true);
        _widget.Click += (_, _) => _app.SetWidgetVisible(!_app.WidgetVisible);
        var showCat = new Forms.ToolStripMenuItem("Remind me now");
        showCat.Click += (_, _) => _app.Scheduler.TriggerNow();

        foreach (var (label, duration) in new (string, TimeSpan?)[]
                 {
                     ("For 30 minutes", TimeSpan.FromMinutes(30)),
                     ("For 1 hour", TimeSpan.FromHours(1)),
                     ("For 2 hours", TimeSpan.FromHours(2)),
                     ("Until tomorrow", null),
                 })
        {
            var item = new Forms.ToolStripMenuItem(label);
            item.Click += (_, _) => _app.Scheduler.Pause(duration);
            _pause.DropDownItems.Add(item);
        }
        _pause.DropDown.BackColor = Color.White;
        _resume.Click += (_, _) => _app.Scheduler.Resume();

        _update.Click += (_, _) => _app.ShowMain(dashboard: true);
        _checkUpdates.Click += (_, _) => _app.CheckForUpdatesFromTray();

        var exit = new Forms.ToolStripMenuItem("Exit PawSip");
        exit.Click += (_, _) => _app.ExitApp();

        menu.Items.AddRange(new Forms.ToolStripItem[]
        {
            _status, new Forms.ToolStripSeparator(),
            open, _drink, showCat, _widget, new Forms.ToolStripSeparator(),
            _pause, _resume, new Forms.ToolStripSeparator(),
            _update, _checkUpdates, exit,
        });
        menu.Opening += (_, _) => Refresh();

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Text = "PawSip",
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) _app.ShowMain();
        };
        _icon.BalloonTipClicked += (_, _) => _app.ShowMain();
    }

    public void Show()
    {
        _icon.Visible = true;
        Refresh();
    }

    public void Refresh()
    {
        var h = _app.Hydration;
        var s = _app.Scheduler;
        _status.Text = $"{h.TodayMl:N0} / {h.GoalMl:N0} ml today";
        _drink.Text = $"I drank a glass (+{_app.Store.Settings.GlassMl} ml)";
        _pause.Visible = !s.IsPaused && _app.Store.Settings.RemindersEnabled;
        _resume.Visible = s.IsPaused;
        _widget.Checked = _app.WidgetVisible;

        var update = _app.AvailableUpdate;
        _update.Visible = update != null;
        _update.Text = update == null ? "" : _app.UpdateStage == UpdateStage.Offered ? $"Update to PawSip {update.Version}…" : $"Updating to PawSip {update.Version}…";
        _checkUpdates.Text = _app.IsCheckingForUpdates ? "Checking for updates…" : "Check for updates…";
        _checkUpdates.Enabled = !_app.IsCheckingForUpdates;

        var next = !_app.Store.Settings.RemindersEnabled ? "reminders off"
            : s.IsPaused ? $"paused until {s.PausedUntil:t}"
            : s.NextReminderAt is { } at ? $"next sip {FormatWhen(at)}" : "";
        var tip = $"PawSip · {h.TodayMl:N0}/{h.GoalMl:N0} ml" + (next.Length > 0 ? $"\n{next}" : "");
        _icon.Text = tip.Length > 127 ? tip[..127] : tip;
    }

    public static string FormatWhen(DateTime at) =>
        at.Date == DateTime.Today ? at.ToString("t") : at.Date == DateTime.Today.AddDays(1) ? "tomorrow " + at.ToString("t") : at.ToString("g");

    public void ShowBalloon(string title, string text) =>
        _icon.ShowBalloonTip(6000, title, text, Forms.ToolTipIcon.None);

    private static Icon LoadIcon()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/PawSip.ico"));
            if (info != null)
            {
                using var stream = info.Stream;
                return new Icon(stream, Forms.SystemInformation.SmallIconSize);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Tray icon");
        }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private sealed class PastelColors : Forms.ProfessionalColorTable
    {
        private static readonly Color Hover = Color.FromArgb(0xEA, 0xF5, 0xFF);
        private static readonly Color Line = Color.FromArgb(0xEC, 0xEE, 0xF6);
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color MenuBorder => Line;
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color ImageMarginGradientBegin => Color.White;
        public override Color ImageMarginGradientMiddle => Color.White;
        public override Color ImageMarginGradientEnd => Color.White;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Color.White;
    }
}
