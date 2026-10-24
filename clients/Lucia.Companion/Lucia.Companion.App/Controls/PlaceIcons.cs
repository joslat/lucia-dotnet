using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Authored 24-unit outline icons; no glyph font or platform-dependent emoji.</summary>
public static class PlaceIcons
{
    public static Geometry For(PlaceIcon icon) => icon switch
    {
        PlaceIcon.Sofa => Sofa, PlaceIcon.Garden => Garden, PlaceIcon.Kitchen => Kitchen,
        PlaceIcon.Door => Door, PlaceIcon.Bed => Bed, _ => Home
    };
    public static Geometry Home { get; } = Geometry.Parse("M3,11 L12,3 21,11 M5,10 L5,21 10,21 10,15 14,15 14,21 19,21 19,10");
    public static Geometry Garden { get; } = Geometry.Parse("M12,21 L12,11 M12,15 C3,15 3,5 3,5 C12,5 12,12 12,12 M12,12 C12,3 21,3 21,3 C21,12 12,12 12,12");
    public static Geometry Door { get; } = Geometry.Parse("M5,21 L5,3 18,3 18,21 M2,21 L22,21 M14,12 L15,12");
    public static Geometry Sofa { get; } = Geometry.Parse("M5,11 L5,5 19,5 19,11 M3,10 L6,10 6,15 18,15 18,10 21,10 21,20 3,20 Z M5,20 L5,22 M19,20 L19,22");
    public static Geometry Kitchen { get; } = Geometry.Parse("M3,4 L21,4 21,21 3,21 Z M3,9 L21,9 M7,6 L9,6 M15,6 L17,6 M7,12 L17,12 17,18 7,18 Z");
    public static Geometry Bed { get; } = Geometry.Parse("M3,19 L3,9 21,9 21,19 M3,16 L21,16 M6,9 L6,4 18,4 18,9 M9,6 L9,9 M15,6 L15,9");
}
