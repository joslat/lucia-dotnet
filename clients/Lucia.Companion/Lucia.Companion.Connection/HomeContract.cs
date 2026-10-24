namespace Lucia.Companion.Connection;

internal static class HomeContract
{
    public static IReadOnlyList<HomeSource> Sources(IReadOnlyList<HomeSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count > 2000 || sources.Any(source => source is null) || sources.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() != sources.Count)
        { throw new ArgumentException("The source catalog is invalid."); }
        return Array.AsReadOnly(sources.ToArray());
    }

    public static IReadOnlyList<T> Places<T>(string homeId, DateTimeOffset capturedAt, IReadOnlyList<T> places,
        Func<T, string> placeId, Func<T, IReadOnlyList<HomeSource>> sources) where T : class
    {
        WireValue.Identifier(homeId);
        ArgumentNullException.ThrowIfNull(places);
        if (capturedAt <= DateTimeOffset.UnixEpoch || places.Count > 200 || places.Any(place => place is null)
            || places.Select(placeId).Distinct(StringComparer.Ordinal).Count() != places.Count)
        { throw new ArgumentException("The home catalog is invalid."); }
        Sources(places.SelectMany(sources).ToArray());
        return Array.AsReadOnly(places.ToArray());
    }

    public static void RequireGrant(DeviceSession session, string homeId, IEnumerable<(string Id, IReadOnlyList<HomeSource> Sources)> places)
    {
        if (session.HomeId != homeId) { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
        foreach (var place in places)
        {
            var allowed = session.Places.SingleOrDefault(grant => grant.PlaceId == place.Id);
            if (allowed is null || allowed.Fields.Count == 0 || place.Sources.Any(source => !allowed.Fields.Contains(source.Field)))
            { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
        }
    }
}
