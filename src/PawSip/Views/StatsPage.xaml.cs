using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PawSip.Controls;
using PawSip.Core;

namespace PawSip.Views;

public partial class StatsPage : UserControl
{
    private readonly App _app = App.Current;
    private DateTime _anchor = DateTime.Today;
    private bool _month;

    public StatsPage()
    {
        InitializeComponent();
        // Keep the page where the user scrolled it (don't jump when something inside updates).
        var scroller = Content as ScrollViewer ?? ((Panel)Content).Children.OfType<ScrollViewer>().First();
        ((UIElement)scroller.Content).AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, e) => e.Handled = true));
        WeekMode.Checked += (_, _) => { _month = false; Refresh(); };
        MonthMode.Checked += (_, _) => { _month = true; Refresh(); };
        PrevButton.Click += (_, _) => { _anchor = _month ? _anchor.AddMonths(-1) : _anchor.AddDays(-7); Refresh(); };
        NextButton.Click += (_, _) => { _anchor = _month ? _anchor.AddMonths(1) : _anchor.AddDays(7); Refresh(); };
        TodayButton.Click += (_, _) => { _anchor = DateTime.Today; Refresh(); };
        Loaded += (_, _) =>
        {
            _app.Hydration.Changed += Refresh;
            Refresh();
        };
        Unloaded += (_, _) => _app.Hydration.Changed -= Refresh;
    }

    private static DateTime StartOfWeek(DateTime day)
    {
        var first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var diff = ((int)day.DayOfWeek - (int)first + 7) % 7;
        return day.Date.AddDays(-diff);
    }

    private void Refresh()
    {
        var culture = CultureInfo.CurrentCulture;
        DateTime from, to;
        if (_month)
        {
            from = new DateTime(_anchor.Year, _anchor.Month, 1);
            to = from.AddMonths(1).AddDays(-1);
            RangeText.Text = from.ToString("MMMM yyyy", culture);
        }
        else
        {
            from = StartOfWeek(_anchor);
            to = from.AddDays(6);
            RangeText.Text = from.Month == to.Month
                ? $"{from.ToString("MMM d", culture)} – {to.Day}"
                : $"{from.ToString("MMM d", culture)} – {to.ToString("MMM d", culture)}";
        }
        NextButton.IsEnabled = to < DateTime.Today;
        TodayButton.Visibility = DateTime.Today < from || DateTime.Today > to ? Visibility.Visible : Visibility.Collapsed;

        var days = _app.Hydration.Range(from, to);
        var past = days.Where(d => d.Date <= DateTime.Today).ToList();
        var tracked = past.Where(d => d.Ml > 0).ToList();

        AvgValue.Text = tracked.Count == 0 ? "0 ml" : $"{tracked.Average(d => d.Ml):N0} ml";
        var met = past.Count(d => d.GoalMet);
        MetValue.Text = $"{met} of {past.Count} {(past.Count == 1 ? "day" : "days")}";
        var best = tracked.OrderByDescending(d => d.Ml).FirstOrDefault();
        BestValue.Text = best.Ml > 0 ? $"{best.Date.ToString(_month ? "MMM d" : "ddd", culture)} · {best.Ml / 1000.0:0.#} L" : "—";
        TotalValue.Text = $"{past.Sum(d => d.Ml) / 1000.0:0.#} L";

        var goal = _app.Store.Settings.GoalMl;
        var items = days.Select(d => new BarItem(
            _month ? d.Date.Day.ToString(culture) : d.Date.ToString("ddd", culture),
            $"{d.Date.ToString("ddd, MMM d", culture)} · {d.Ml:N0} ml",
            d.Ml, d.GoalMl > 0 ? d.GoalMl : goal, d.Date == DateTime.Today)).ToList();
        Chart.ShowAllLabels = !_month;
        Chart.SetItems(items, _app.Store.Settings.Animations);
        ChartTitle.Text = _month ? "Daily intake this month" : "Daily intake this week";

        CalendarCard.Visibility = _month ? Visibility.Visible : Visibility.Collapsed;
        if (_month) Heatmap.SetMonth(from, days);
    }
}
