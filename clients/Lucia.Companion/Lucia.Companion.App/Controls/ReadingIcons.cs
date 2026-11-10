using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Authored 24-unit outline paths for reading types, drawn with the shared 1.7 stroke. Unknown types use a plain dot.</summary>
public static class ReadingIcons
{
    public static Geometry Temperature { get; } = Geometry.Parse("M10,4 C10,2.9 14,2.9 14,4 L14,14 C16,15.2 16.4,18.5 14.6,20.2 C13.2,21.6 10.8,21.6 9.4,20.2 C7.6,18.5 8,15.2 10,14 Z M12,9 L12,17");
    public static Geometry Drop { get; } = Geometry.Parse("M12,3 C12,3 6,10 6,14.5 C6,18 8.7,21 12,21 C15.3,21 18,18 18,14.5 C18,10 12,3 12,3 Z");
    public static Geometry Light { get; } = Geometry.Parse("M9,18 L15,18 M10,21 L14,21 M9,15.5 C7,14 6,12.3 6,10 C6,6.7 8.7,4 12,4 C15.3,4 18,6.7 18,10 C18,12.3 17,14 15,15.5 Z");
    public static Geometry Contact => PlaceIcons.Door;
    public static Geometry Motion { get; } = Geometry.Parse("M12,12 L12.1,12 M8.5,8.5 C6.6,10.4 6.6,13.6 8.5,15.5 M15.5,8.5 C17.4,10.4 17.4,13.6 15.5,15.5 M5.6,5.6 C2.5,8.7 2.5,15.3 5.6,18.4 M18.4,5.6 C21.5,8.7 21.5,15.3 18.4,18.4");
    public static Geometry Other { get; } = Geometry.Parse("M10,12 A2,2 0 1 0 14,12 A2,2 0 1 0 10,12 Z");

    public static Geometry For(ObservationRole? role) => role switch
    {
        ObservationRole.Temperature => Temperature,
        ObservationRole.Humidity or ObservationRole.Moisture or ObservationRole.Leak => Drop,
        ObservationRole.Light => Light,
        ObservationRole.Contact => Contact,
        ObservationRole.Motion => Motion,
        _ => Other,
    };
}
