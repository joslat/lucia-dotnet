using System.ComponentModel;
using System.Globalization;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.ViewModels;

/// <summary>
/// Which day Story shows, or the whole week. Days come from a supplied source and are built once each. The place filter
/// follows the person between days and into the week.
/// </summary>
public sealed class StoryNavigator : INotifyPropertyChanged
{
    private readonly Func<DateOnly, StoryDay> source;
    private readonly Dictionary<DateOnly, StoryDay> days = [];

    public StoryNavigator(IReadOnlyList<DateOnly> dates, DateOnly today, Func<DateOnly, StoryDay> dayFor)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(dayFor);
        if (dates.Count == 0 || !dates.Contains(today)) { throw new ArgumentException("Today must be one of the available dates.", nameof(today)); }
        Dates = dates.Order().ToArray();
        Today = today;
        source = dayFor;
        Date = today;
        Day = new StoryViewModel(DayFor(today));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<DateOnly> Dates { get; }
    public DateOnly Today { get; }
    /// <summary>How many places need the person now; shown only with today, since it describes the present.</summary>
    public int AttentionCount { get; init; }
    public DateOnly Date { get; private set; }
    public StoryMode Mode { get; private set; }
    public StoryViewModel Day { get; private set; }
    public bool CanGoPrevious => Mode == StoryMode.Day && Date > Dates[0];
    public bool CanGoNext => Mode == StoryMode.Day && Date < Dates[^1];

    public string DateLabel => Mode == StoryMode.Week
        ? $"{Dates[0].Day}–{Dates[^1].ToString("d MMM", CultureInfo.InvariantCulture)}"
        : Date == Today ? $"Today · {Short(Date)}" : Date == Today.AddDays(-1) ? $"Yesterday · {Short(Date)}" : Short(Date);

    public IReadOnlyList<StoryWeekDay> Week => Dates.Select(date =>
    {
        var day = DayFor(date);
        var label = date.ToString("ddd d", CultureInfo.InvariantCulture) + (date == Today ? " · today" : string.Empty);
        var intervals = day.Intervals.Where(interval => Day.SelectedPlaceId is null || interval.PlaceId == Day.SelectedPlaceId).ToArray();
        return new StoryWeekDay(date, label, day, intervals);
    }).ToArray();

    public void Previous() { if (CanGoPrevious) { ShowDay(Dates[Dates.ToList().IndexOf(Date) - 1]); } }
    public void Next() { if (CanGoNext) { ShowDay(Dates[Dates.ToList().IndexOf(Date) + 1]); } }

    public void ShowDay(DateOnly date)
    {
        if (!Dates.Contains(date)) { throw new ArgumentOutOfRangeException(nameof(date)); }
        var place = Day.SelectedPlaceId;
        if (date != Date)
        {
            Date = date;
            Day = new StoryViewModel(DayFor(date));
            if (place is not null && Day.Day.Places.Any(item => item.Id == place)) { Day.SelectPlace(place); }
        }
        Mode = StoryMode.Day;
        Notify();
    }

    public void SetMode(StoryMode mode)
    {
        if (!Enum.IsDefined(mode)) { throw new ArgumentOutOfRangeException(nameof(mode)); }
        if (mode == Mode) { return; }
        Mode = mode;
        Notify();
    }

    private StoryDay DayFor(DateOnly date)
    {
        if (!days.TryGetValue(date, out var day)) { days[date] = day = source(date); }
        return day;
    }

    private static string Short(DateOnly date) => date.ToString("ddd d MMM", CultureInfo.InvariantCulture);
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
