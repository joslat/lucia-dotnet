namespace Lucia.Companion.Core;

/// <summary>Readable zone labels and the setup chooser's choices, with "No zone" first.</summary>
public static class PlaceZones
{
    public static IReadOnlyList<string> Choices { get; } = Array.AsReadOnly(new[] { "No zone", "Inside", "Outside", "Own building" });

    public static string Label(PlaceZone? zone) => zone switch
    {
        null => "No zone",
        PlaceZone.Inside => "Inside",
        PlaceZone.Outside => "Outside",
        PlaceZone.OwnBuilding => "Own building",
        _ => throw new ArgumentOutOfRangeException(nameof(zone)),
    };

    public static int ChoiceIndex(PlaceZone? zone) => zone is null ? 0 : (int)zone.Value + 1;

    public static PlaceZone? FromChoice(int index) => index <= 0 || index >= Choices.Count ? null : (PlaceZone)(index - 1);
}
