namespace Lucia.Companion.Connection;

public sealed class HomePlaceCatalog
{
    public HomePlaceCatalog(string placeId, string name, IReadOnlyList<HomeSource> sources)
    {
        PlaceId = WireValue.Identifier(placeId);
        Name = WireValue.Label(name);
        Sources = HomeContract.Sources(sources);
    }
    public string PlaceId { get; }
    public string Name { get; }
    public IReadOnlyList<HomeSource> Sources { get; }
}
