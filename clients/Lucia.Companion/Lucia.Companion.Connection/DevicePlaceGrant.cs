using Lucia.Companion.Core;

namespace Lucia.Companion.Connection;

public sealed class DevicePlaceGrant
{
    public DevicePlaceGrant(string placeId, IReadOnlyList<ObservationRole> fields)
    {
        PlaceId = WireValue.Identifier(placeId);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count > 7 || fields.Any(field => !Enum.IsDefined(field)) || fields.Distinct().Count() != fields.Count)
        { throw new ArgumentException("Observation fields are invalid."); }
        Fields = Array.AsReadOnly(fields.ToArray());
    }
    public string PlaceId { get; }
    public IReadOnlyList<ObservationRole> Fields { get; }
}
