namespace lucia.Agents.Companion;

/// <summary>Owner-assigned read authority, never inferred from names, client requests or physical neighbors.</summary>
public sealed class CompanionGrant
{
    public CompanionGrant(string homeId, string memberId, IReadOnlyList<CompanionPlaceGrant> places,
        bool canReadActivity = false, bool canReadHistory = false, bool canReadEngine = false)
    {
        HomeId = CompanionIdentifier.Validate(homeId);
        MemberId = CompanionIdentifier.Validate(memberId);
        ArgumentNullException.ThrowIfNull(places);
        if (places.Count > 200 || places.Any(place => place is null) || places.Select(place => place.PlaceId).Distinct(StringComparer.Ordinal).Count() != places.Count)
        {
            throw new ArgumentException("A grant supports at most 200 unique places.", nameof(places));
        }
        Places = Array.AsReadOnly(places.ToArray());
        CanReadActivity = canReadActivity;
        CanReadHistory = canReadHistory;
        CanReadEngine = canReadEngine;
    }
    public string HomeId { get; }
    public string MemberId { get; }
    public IReadOnlyList<CompanionPlaceGrant> Places { get; }
    public bool CanReadActivity { get; }
    public bool CanReadHistory { get; }
    public bool CanReadEngine { get; }

    public bool Allows(string homeId, string placeId, CompanionField field) => HomeId == homeId && Enum.IsDefined(field)
        && Places.Any(place => place.PlaceId == placeId && place.Fields.Contains(field));
}
