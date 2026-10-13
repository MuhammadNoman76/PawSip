using System.Diagnostics;
using System.Windows.Threading;
using PawSip.Core;
using PawSip.Interop;

namespace PawSip;

/// <summary>Where an offered update is: on offer, downloading, or handed to the installer.</summary>
public enum UpdateStage { None, Offered, Downloading, Installing }

/// <summary>What a "Check for updates" found.</summary>
public enum UpdateCheckResult { UpToDate, Available, Failed, Busy }

/// <summary>
/// Automatic updates: a quiet check about 30 s after start-up and then at most once a day. A newer version
/// is offered in a card on the dashboard, or with a tray notification while the window is closed (never over a
/// fullscreen app). "Update now" downloads and verifies the installer, runs it silently and exits; the installer
/// starts PawSip again.
/// </summary>
public partial class App
{
    private const string DownloadPageUrl = "https://www.nomanshafiq.com/pawsip/";
    private static readonly TimeSpan FirstUpdateCheckDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan UpdateRetryDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan UpdateTimerTick = TimeSpan.FromMinutes(1);

    private DispatcherTimer? _updateTimer;
    private DateTime _nextUpdateAttemptUtc;   // after a failed check (offline): wait a while before trying again
    private bool _notifyUpdatePending;        // found while the window was closed: notify at a good moment
    private CancellationTokenSource? _updateDownload;

    /// <summary>The newer version on offer (null when there is none, or after Later / Skip).</summary>
    public UpdateManifest? AvailableUpdate { get; private set; }

    public UpdateStage UpdateStage { get; private set; }

    /// <summary>Download progress, 0 to 1.</summary>
    public double UpdateProgress { get; private set; }

    /// <summary>Why the last download didn't work, shown on the update card.</summary>
    public string? UpdateError { get; private set; }

    public bool IsCheckingForUpdates { get; private set; }

    /// <summary>Anything about the update offer changed (the dashboard card, the tray menu and Settings follow it).</summary>
    public event Action? UpdateChanged;

    private void InitUpdates(bool justUpdated)
    {
        // An installer from an earlier update (it may still be finishing, so this retries for a while).
        _ = Task.Run(Updater.CleanUpDownloadsAsync);
        UpdateChanged += () => _tray?.Refresh();

        if (justUpdated && CanNotifyNow())
            _tray?.ShowBalloon($"PawSip {Updater.CurrentVersionText} is ready", "Updated and back in the tray. Meow!");

        _updateTimer = new DispatcherTimer { Interval = FirstUpdateCheckDelay };
        _updateTimer.Tick += (_, _) =>
        {
            // First tick 30 s after start-up, then every minute; the feed itself is read at most once a day.
            _updateTimer.Interval = UpdateTimerTick;
            if (_notifyUpdatePending) TryNotifyUpdate();
            if (Store.Settings.CheckForUpdates && UpdateCheckDue()) _ = CheckForUpdatesAsync(manual: false);
        };
        _updateTimer.Start();
    }

    private bool UpdateCheckDue()
    {
        var now = DateTime.UtcNow;
        if (now < _nextUpdateAttemptUtc) return false;
        return Store.Data.LastUpdateCheck is not { } last || last > now || now - last >= UpdateCheckInterval;
    }

    /// <summary>
    /// Reads the feed. Automatic checks are silent unless there's something new; manual ones (Settings, tray)
    /// get the result back, and also offer a version the user skipped.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool manual)
    {
        if (IsCheckingForUpdates || UpdateStage is UpdateStage.Downloading or UpdateStage.Installing) return UpdateCheckResult.Busy;

        IsCheckingForUpdates = true;
        _nextUpdateAttemptUtc = DateTime.UtcNow + UpdateRetryDelay;
        UpdateChanged?.Invoke();
        try
        {
            UpdateManifest manifest;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                manifest = await Updater.FetchManifestAsync(cts.Token);

            Store.Data.LastUpdateCheck = DateTime.UtcNow;
            Store.Save();

            if (!Updater.IsNewer(manifest))
            {
                ClearOffer();
                return UpdateCheckResult.UpToDate;
            }

            // A skipped version is only offered again when asked for.
            var skipped = Updater.ParseVersion(Store.Settings.SkippedVersion) is { } s && s == Updater.ParseVersion(manifest.Version);
            if (skipped && !manual) return UpdateCheckResult.UpToDate;

            OfferUpdate(manifest, manual);
            return UpdateCheckResult.Available;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update check (" + Updater.FeedUrl + ")");
            return UpdateCheckResult.Failed;
        }
        finally
        {
            IsCheckingForUpdates = false;
            UpdateChanged?.Invoke();
        }
    }

    private void OfferUpdate(UpdateManifest manifest, bool manual)
    {
        AvailableUpdate = manifest;
        UpdateStage = UpdateStage.Offered;
        UpdateError = null;
        UpdateProgress = 0;
        UpdateChanged?.Invoke();

        // With the window open, the dashboard card is enough. Otherwise a tray notification, at a good moment.
        _notifyUpdatePending = !manual && _main == null;
        if (_notifyUpdatePending) TryNotifyUpdate();
    }

    private void TryNotifyUpdate()
    {
        if (AvailableUpdate is not { } update || UpdateStage != UpdateStage.Offered || _main != null)
        {
            _notifyUpdatePending = false;
            return;
        }
        if (!CanNotifyNow()) return; // fullscreen app, locked, or away: try again on the next tick
        _notifyUpdatePending = false;
        _tray?.ShowBalloon($"PawSip {update.Version} is available", "Click to see what's new and update. You can also update from the tray menu.");
    }

    /// <summary>Only notify someone who is at the PC and not in a fullscreen game, video or presentation.</summary>
    private static bool CanNotifyNow() =>
        Presence.DisplayOn && !Presence.Locked && Native.IdleTime < Presence.ActiveWithin && !Native.IsUserBusy();

    /// <summary>Tray "Check for updates…": answers with a notification, or opens the dashboard with the offer.</summary>
    public async void CheckForUpdatesFromTray()
    {
        if (UpdateStage is UpdateStage.Downloading or UpdateStage.Installing)
        {
            ShowMain(dashboard: true);
            return;
        }
        var result = await CheckForUpdatesAsync(manual: true);
        switch (result)
        {
            case UpdateCheckResult.Available:
                ShowMain(dashboard: true);
                break;
            case UpdateCheckResult.UpToDate:
                _tray?.ShowBalloon("You're up to date", $"PawSip {Updater.CurrentVersionText} is the latest version.");
                break;
            case UpdateCheckResult.Failed:
                _tray?.ShowBalloon("Couldn't check for updates", "Check your internet connection and try again.");
                break;
        }
    }

    private void ClearOffer()
    {
        AvailableUpdate = null;
        UpdateStage = UpdateStage.None;
        UpdateError = null;
        _notifyUpdatePending = false;
        UpdateChanged?.Invoke();
    }

    /// <summary>"Later": hide the offer until the next daily check.</summary>
    public void PostponeUpdate()
    {
        if (UpdateStage is UpdateStage.Downloading or UpdateStage.Installing) return;
        ClearOffer();
    }

    /// <summary>"Skip this version": don't offer it again automatically (Check for updates still does).</summary>
    public void SkipUpdate()
    {
        if (AvailableUpdate == null || UpdateStage is UpdateStage.Downloading or UpdateStage.Installing) return;
        Store.Settings.SkippedVersion = AvailableUpdate.Version;
        Store.Save();
        ClearOffer();
    }

    public void CancelUpdateDownload() => _updateDownload?.Cancel();

    /// <summary>The installer can only update a copy that it installed, and only with an installer for this edition.</summary>
    public bool CanInstallUpdate => AvailableUpdate?.ForThisEdition != null && Updater.IsInstalled();

    /// <summary>"Update now": download, verify, run the installer silently and exit (the installer starts PawSip again).</summary>
    public async Task UpdateNowAsync()
    {
        var manifest = AvailableUpdate;
        if (manifest == null || UpdateStage != UpdateStage.Offered) return;

        var file = manifest.ForThisEdition;
        if (file == null || !Updater.IsInstalled())
        {
            // Not installed by the installer (or no installer for this edition): let the user download it.
            OpenInBrowser(string.IsNullOrWhiteSpace(manifest.Page) ? DownloadPageUrl : manifest.Page!);
            ClearOffer();
            return;
        }

        UpdateStage = UpdateStage.Downloading;
        UpdateProgress = 0;
        UpdateError = null;
        UpdateChanged?.Invoke();

        _updateDownload = new CancellationTokenSource();
        try
        {
            var progress = new Progress<double>(f =>
            {
                UpdateProgress = f;
                UpdateChanged?.Invoke();
            });
            var installer = await Updater.DownloadAsync(manifest, file, progress, _updateDownload.Token);

            UpdateStage = UpdateStage.Installing;
            UpdateProgress = 1;
            UpdateChanged?.Invoke();
            Updater.LaunchInstaller(installer);

            // Exit so the installer can replace our files (it starts PawSip again when it's done).
            await Task.Delay(600);
            ExitApp();
        }
        catch (OperationCanceledException)
        {
            UpdateStage = UpdateStage.Offered;
            UpdateError = "Download cancelled.";
            UpdateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update download / install");
            UpdateStage = UpdateStage.Offered;
            UpdateError = ex is UpdateVerificationException
                ? ex.Message + " The file was deleted."
                : "Couldn't download the update: " + ex.Message;
            UpdateChanged?.Invoke();
            if (_main == null && CanNotifyNow()) _tray?.ShowBalloon("PawSip couldn't update", "Open PawSip to try again.");
        }
        finally
        {
            _updateDownload?.Dispose();
            _updateDownload = null;
        }
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Open " + url);
        }
    }

    private void StopUpdates()
    {
        _updateTimer?.Stop();
        _updateDownload?.Cancel();
    }
}
