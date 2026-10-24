namespace lucia.Agents.Companion;

public sealed class CompanionPlaceGrant
{
    public CompanionPlaceGrant(string placeId, IReadOnlyList<CompanionField> fields)
    {
        PlaceId = CompanionIdentifier.Validate(placeId);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count > 7 || fields.Any(field => !Enum.IsDefined(field)) || fields.Distinct().Count() != fields.Count)
        {
            throw new ArgumentException("Observation fields must be defined and unique.", nameof(fields));
        }
        Fields = Array.AsReadOnly(fields.ToArray());
    }
    public string PlaceId { get; }
    public IReadOnlyList<CompanionField> Fields { get; }
}
