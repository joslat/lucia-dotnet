using Lucia.Companion.Core;

namespace Lucia.Companion.App.ViewModels;

/// <summary>One day in the Week view: its reports (filtered to the chosen place, if any) and how many were recorded.</summary>
public sealed record StoryWeekDay(DateOnly Date, string Label, StoryDay Day, IReadOnlyList<StoryInterval> Intervals)
{
    public int ReportCount => Intervals.Count(interval => interval.Quality == StoryQuality.Recorded);
}
