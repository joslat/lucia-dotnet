namespace Lucia.Companion.Core;

/// <summary>Explicit metadata connecting a source role to one place, independent of adjacency.</summary>
public sealed record ObservationBinding
{
    public ObservationBinding(string placeId, string sourceId, ObservationRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeId);
        ObservationSource.ValidateId(sourceId);
        if (!Enum.IsDefined(role)) { throw new ArgumentOutOfRangeException(nameof(role)); }
        PlaceId = placeId;
        SourceId = sourceId;
        Role = role;
    }
    public string PlaceId { get; }
    public string SourceId { get; }
    public ObservationRole Role { get; }

    public static string MissingReadingsReason(bool hasBindings) => hasBindings
        ? "Observation bindings configured. No readings available."
        : "No observation bindings configured for this place.";
}
