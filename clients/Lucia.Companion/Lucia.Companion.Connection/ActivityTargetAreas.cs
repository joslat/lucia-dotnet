namespace Lucia.Companion.Connection;

public sealed class ActivityTargetAreas
{
    public ActivityTargetAreas(DateTimeOffset checkedAt, bool isPartial, IReadOnlyList<ActivityTargetArea> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        if (checkedAt <= DateTimeOffset.UnixEpoch || places.Count is < 1 or > 6 || places.Any(place => place is null)
            || places.Select(place => place.PlaceId).Distinct(StringComparer.Ordinal).Count() != places.Count)
        { throw new ArgumentException("The current target area projection is invalid."); }
        CheckedAt = checkedAt; IsPartial = isPartial; Places = Array.AsReadOnly(places.ToArray());
    }
    public DateTimeOffset CheckedAt { get; }
    public bool IsPartial { get; }
    public IReadOnlyList<ActivityTargetArea> Places { get; }
}
