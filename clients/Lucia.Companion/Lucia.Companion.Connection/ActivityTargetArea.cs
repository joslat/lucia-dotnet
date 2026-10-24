namespace Lucia.Companion.Connection;

/// <summary>Current authorized registry area for a reported requested target, not request origin.</summary>
public sealed class ActivityTargetArea
{
    public ActivityTargetArea(string placeId, string name) { PlaceId = WireValue.Identifier(placeId); Name = WireValue.Label(name); }
    public string PlaceId { get; }
    public string Name { get; }
}
