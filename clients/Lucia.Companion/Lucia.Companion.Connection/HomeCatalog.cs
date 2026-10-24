namespace Lucia.Companion.Connection;

public sealed class HomeCatalog
{
    public HomeCatalog(string homeId, DateTimeOffset capturedAt, IReadOnlyList<HomePlaceCatalog> places)
    {
        Places = HomeContract.Places(homeId, capturedAt, places, place => place.PlaceId, place => place.Sources);
        HomeId = homeId; CapturedAt = capturedAt;
    }
    public string HomeId { get; }
    public DateTimeOffset CapturedAt { get; }
    public IReadOnlyList<HomePlaceCatalog> Places { get; }
    internal void RequireGrant(DeviceSession session) => HomeContract.RequireGrant(session, HomeId, Places.Select(place => (place.PlaceId, place.Sources)));
}
