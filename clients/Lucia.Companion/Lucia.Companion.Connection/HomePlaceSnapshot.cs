namespace Lucia.Companion.Connection;

public sealed class HomePlaceSnapshot
{
    public HomePlaceSnapshot(string placeId, string name, IReadOnlyList<HomeSource> sources, IReadOnlyList<HomeReading> readings)
    {
        PlaceId = WireValue.Identifier(placeId);
        Name = WireValue.Label(name);
        Sources = HomeContract.Sources(sources);
        ArgumentNullException.ThrowIfNull(readings);
        var byId = Sources.ToDictionary(source => source.SourceId, StringComparer.Ordinal);
        if (readings.Count != Sources.Count || readings.Any(reading => reading is null || !byId.TryGetValue(reading.SourceId, out var source) || reading.Field != source.Field)
            || readings.Select(reading => reading.SourceId).Distinct(StringComparer.Ordinal).Count() != readings.Count)
        { throw new ArgumentException("Reports must correspond to unique catalog sources."); }
        Readings = Array.AsReadOnly(readings.ToArray());
    }
    public string PlaceId { get; }
    public string Name { get; }
    public IReadOnlyList<HomeSource> Sources { get; }
    public IReadOnlyList<HomeReading> Readings { get; }
}
