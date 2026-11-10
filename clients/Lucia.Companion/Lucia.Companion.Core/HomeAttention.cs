namespace Lucia.Companion.Core;

/// <summary>
/// Wording for Home's "things need you" header and list. Counts and ages come only from supplied
/// place states and reading times; nothing here concludes that a home is safe, empty or occupied.
/// </summary>
public static class HomeAttention
{
    public static string Headline(int count) => count switch
    {
        <= 0 => "Nothing needs you",
        1 => "1 thing needs you",
        _ => $"{count} things need you",
    };

    /// <param name="quietCount">Places that are quiet. Only these have "nothing to report"; changed or working places do report something.</param>
    public static string Subline(string? firstPlaceName, int attentionCount, int placeCount, bool isDemo, int? quietCount = null)
    {
        if (placeCount <= 0) { return "No places are set up yet."; }
        if (attentionCount <= 0 || firstPlaceName is null)
        {
            if (!isDemo) { return "Your configured reports flag nothing. That isn't a safety or presence check."; }
            var reporting = placeCount - Math.Clamp(quietCount ?? placeCount, 0, placeCount);
            return reporting == 0
                ? $"None of the {placeCount} places in this sample has anything to report."
                : $"Nothing in this sample needs you. {reporting} of {placeCount} places report changes or activity.";
        }
        var others = placeCount - attentionCount;
        var quiet = Math.Clamp(quietCount ?? others, 0, Math.Max(0, others));
        if (others <= 0) { return $"{firstPlaceName} first."; }
        if (quiet == others)
        {
            return others == 1 ? $"{firstPlaceName} first. The other place has nothing to report." : $"{firstPlaceName} first. The other {others} places have nothing to report.";
        }
        if (quiet == 0)
        {
            return others == 1 ? $"{firstPlaceName} first. The other place reports a change or activity." : $"{firstPlaceName} first. The other {others} places report changes or activity.";
        }
        return $"{firstPlaceName} first. {quiet} of the other {others} places {(quiet == 1 ? "has" : "have")} nothing to report.";
    }

    public static string KindLabel(PlaceState state) => state switch
    {
        PlaceState.Problem => "Reported problem",
        PlaceState.Waiting => "Waiting for you",
        PlaceState.Unavailable => "Data unavailable",
        _ => state.ToString(),
    };

    /// <summary>Age of a reading at the time the reports were captured. A reading stamped after capture reads as just now.</summary>
    public static string? Age(DateTimeOffset? at, DateTimeOffset? reference)
    {
        if (at is not { } time || reference is not { } now) { return null; }
        var elapsed = now - time;
        if (elapsed < TimeSpan.FromMinutes(1)) { return "just now"; }
        if (elapsed < TimeSpan.FromHours(1)) { return $"{(int)elapsed.TotalMinutes} min ago"; }
        if (elapsed < TimeSpan.FromDays(1))
        {
            var minutes = elapsed.Minutes;
            return minutes == 0 ? $"{(int)elapsed.TotalHours} h ago" : $"{(int)elapsed.TotalHours} h {minutes} min ago";
        }
        var days = (int)elapsed.TotalDays;
        return days == 1 ? "1 day ago" : $"{days} days ago";
    }

    public static DateTimeOffset? LatestReadingAt(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var times = place.Observations.Where(item => item.ObservedAt.HasValue).Select(item => item.ObservedAt!.Value).ToArray();
        return times.Length == 0 ? null : times.Max();
    }

    public static string ReadingLine(Place place, DateTimeOffset? reference) =>
        Age(LatestReadingAt(place), reference) is { } age ? $"Latest reading {age}" : "No reading time";
}
