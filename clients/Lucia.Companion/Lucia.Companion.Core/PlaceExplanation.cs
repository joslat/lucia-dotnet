using System.Globalization;

namespace Lucia.Companion.Core;

/// <summary>
/// The explanation card on a place's page: what the reports say, and why the place is shown the way it is.
/// It restates supplied readings and states; it never concludes a physical result, safety or presence.
/// </summary>
public static class PlaceExplanation
{
    public static string Body(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var current = place.Observations.Where(item => item.Quality == ObservationQuality.Current && item.Value is not null && item.ObservedAt.HasValue).ToArray();
        switch (place.State)
        {
            case PlaceState.Unavailable:
                var last = place.Observations.Where(item => item.Quality != ObservationQuality.Current && item.Value is not null && item.ObservedAt.HasValue)
                    .OrderByDescending(item => item.ObservedAt).FirstOrDefault();
                return last is null
                    ? "No configured source has a current reading, so nothing here is shown as normal."
                    : $"Its last reading was {last.Value} at {Time(last.ObservedAt!.Value)}. The current value isn't known, so it is shown as unavailable, not as normal.";
            case PlaceState.Problem:
                var report = current.FirstOrDefault(item => item.Role == ObservationRole.Leak) ?? current.FirstOrDefault();
                return report is null
                    ? "A configured source reports a problem. It is a report, not a confirmed fault."
                    : $"{report.Label} reads {report.Value} at {Time(report.ObservedAt!.Value)}. It is a source report, not a confirmed fault.";
            case PlaceState.Waiting:
                return "Lucia is waiting for your answer before it continues. Nothing happens until you respond.";
            case PlaceState.Working:
                return "Lucia is reading this place's sources. Reading doesn't change anything here.";
            case PlaceState.Changed:
                var latest = HomeAttention.LatestReadingAt(place);
                var caption = PlaceCaption.Caption(place);
                return caption is null || latest is null
                    ? "A source reports a change. A change is a report, not proof that someone is there."
                    : $"{caption}, reported at {Time(latest.Value)}. A change is a report, not proof that someone is there.";
            default:
                return "Recent reports with nothing flagged. Quiet isn't a safety or presence check.";
        }
    }

    public static string WhyShown(PlaceState state) => state switch
    {
        PlaceState.Problem => "Shown because a configured source reports a problem. It is a report, not a confirmed fault.",
        PlaceState.Waiting => "Shown because a request is waiting for your answer.",
        PlaceState.Unavailable => "Shown because a configured source stopped reporting or isn't current.",
        PlaceState.Changed => "Shown as changed because a contact or motion source reports activity, not presence.",
        PlaceState.Working => "Shown because Lucia is reading this place's sources.",
        _ => "Shown as quiet because its sources reported recently and flagged nothing.",
    };

    private static string Time(DateTimeOffset at) => at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
