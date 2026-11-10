using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Authored 24-unit outline paths for the kinds of attention: a fault, a choice waiting for you, a source gone silent.</summary>
public static class AttentionIcons
{
    public static Geometry Fault { get; } = Geometry.Parse("M12,4 L21.5,20 2.5,20 Z M12,10 L12,14.5 M12,17 L12,17.2");
    public static Geometry Choice { get; } = Geometry.Parse("M4,5 L20,5 20,16 11,16 7,20 7,16 4,16 Z M8.5,10.5 L8.6,10.5 M12,10.5 L12.1,10.5 M15.5,10.5 L15.6,10.5");
    public static Geometry Silent { get; } = Geometry.Parse("M9.5,9.5 C9.5,6.2 14.5,6.2 14.5,9.5 C14.5,11.6 12,11.8 12,14 M12,17 L12,17.2");
    public static Geometry Chevron { get; } = Geometry.Parse("M9,6 L15,12 9,18");

    public static Geometry For(PlaceState state) => state switch
    {
        PlaceState.Problem => Fault,
        PlaceState.Waiting => Choice,
        _ => Silent,
    };
}
