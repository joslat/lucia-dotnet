namespace Lucia.Companion.Connection;

public sealed class DeviceSession
{
    public DeviceSession(string sessionId, string deviceName, DateTimeOffset expiresAt, string homeId, string memberId,
        IReadOnlyList<DevicePlaceGrant> places, bool canReadActivity = false, bool canReadHistory = false, bool canReadEngine = false)
    {
        SessionId = WireValue.Identifier(sessionId);
        DeviceName = WireValue.DeviceName(deviceName);
        HomeId = WireValue.Identifier(homeId);
        MemberId = WireValue.Identifier(memberId);
        ArgumentNullException.ThrowIfNull(places);
        if (places.Count > 200 || places.Any(place => place is null) || places.Select(place => place.PlaceId).Distinct(StringComparer.Ordinal).Count() != places.Count
            || expiresAt <= DateTimeOffset.UnixEpoch)
        { throw new ArgumentException("The device grant is invalid."); }
        Places = Array.AsReadOnly(places.ToArray());
        ExpiresAt = expiresAt;
        CanReadActivity = canReadActivity;
        CanReadHistory = canReadHistory;
        CanReadEngine = canReadEngine;
    }
    public string SessionId { get; }
    public string DeviceName { get; }
    public DateTimeOffset ExpiresAt { get; }
    public string HomeId { get; }
    public string MemberId { get; }
    public IReadOnlyList<DevicePlaceGrant> Places { get; }
    public bool CanReadActivity { get; }
    public bool CanReadHistory { get; }
    public bool CanReadEngine { get; }
}
