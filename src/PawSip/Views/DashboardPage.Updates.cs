using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PawSip.Core;

namespace PawSip.Views;

/// <summary>The update offer card at the top of the dashboard (state lives in App.Updates.cs).</summary>
public partial class DashboardPage
{
    private void InitUpdateCard()
    {
        UpdateNowButton.Click += (_, _) => _ = _app.UpdateNowAsync();
        UpdateLaterButton.Click += (_, _) => _app.PostponeUpdate();
        UpdateSkipButton.Click += (_, _) => _app.SkipUpdate();
        UpdateCancelButton.Click += (_, _) => _app.CancelUpdateDownload();

        Loaded += (_, _) =>
        {
            _app.UpdateChanged += RefreshUpdateCard;
            RefreshUpdateCard();
        };
        Unloaded += (_, _) => _app.UpdateChanged -= RefreshUpdateCard;
    }

    private void RefreshUpdateCard()
    {
        var update = _app.AvailableUpdate;
        var stage = _app.UpdateStage;
        if (update == null || stage == UpdateStage.None)
        {
            UpdateCard.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateTitle.Text = $"PawSip {update.Version} is here";
        var subtitle = $"You have {Updater.CurrentVersionText}";
        if (DateTime.TryParseExact(update.Released, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var released))
            subtitle += " · released " + released.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        UpdateSubtitle.Text = subtitle;

        // The notes only change with a new offer.
        if (!ReferenceEquals(UpdateNotes.Tag, update))
        {
            UpdateNotes.Tag = update;
            UpdateNotes.Children.Clear();
            foreach (var note in update.Notes.Take(6))
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                var bullet = new TextBlock { Text = "•", FontSize = 13.5, Foreground = (Brush)FindResource("Water"), Margin = new Thickness(1, 0, 9, 0) };
                DockPanel.SetDock(bullet, Dock.Left);
                row.Children.Add(bullet);
                row.Children.Add(new TextBlock { Text = note, FontSize = 13.5, Foreground = (Brush)FindResource("Ink2"), TextWrapping = TextWrapping.Wrap });
                UpdateNotes.Children.Add(row);
            }
            UpdateNotes.Visibility = UpdateNotes.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        var busy = stage is UpdateStage.Downloading or UpdateStage.Installing;
        UpdateProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateNowButton.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        UpdateLaterButton.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        UpdateSkipButton.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        UpdateCancelButton.Visibility = stage == UpdateStage.Downloading ? Visibility.Visible : Visibility.Collapsed;

        if (busy)
        {
            var fraction = MathEx.Clamp(_app.UpdateProgress, 0, 1);
            UpdateProgressDone.Width = new GridLength(fraction, GridUnitType.Star);
            UpdateProgressLeft.Width = new GridLength(1 - fraction, GridUnitType.Star);
            var sizeMb = (update.ForThisEdition?.Size ?? 0) / (1024.0 * 1024.0);
            UpdateProgressText.Text = stage == UpdateStage.Installing
                ? "Verified. Installing… PawSip will be back in the tray in a moment."
                : $"Downloading PawSip {update.Version}… {fraction * 100:0}% of {sizeMb:0.0} MB";
        }
        else
        {
            UpdateNowButton.Content = !_app.CanInstallUpdate ? "Download page" : _app.UpdateError != null ? "Try again" : "Update now";
        }

        UpdateStatus.Text = _app.UpdateError ?? "";
        UpdateStatus.Visibility = !busy && _app.UpdateError != null ? Visibility.Visible : Visibility.Collapsed;
        UpdateCard.Visibility = Visibility.Visible;
    }
}
