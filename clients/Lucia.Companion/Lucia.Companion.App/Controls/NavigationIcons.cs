using Avalonia.Media;

namespace Lucia.Companion.App.Controls;

/// <summary>Authored 24-unit outline paths for the navigation destinations, drawn with the shared 1.7 stroke.</summary>
public static class NavigationIcons
{
    public static Geometry Home => PlaceIcons.Home;
    public static Geometry Story { get; } = Geometry.Parse("M3,12 A9,9 0 1 0 21,12 A9,9 0 1 0 3,12 Z M12,7 L12,12 15.5,14");
    public static Geometry Lucia { get; } = Geometry.Parse("M12,3 C12.7,8.6 15.4,11.3 21,12 C15.4,12.7 12.7,15.4 12,21 C11.3,15.4 8.6,12.7 3,12 C8.6,11.3 11.3,8.6 12,3 Z");
    public static Geometry More { get; } = Geometry.Parse("M4.5,12 A1.2,1.2 0 1 0 6.9,12 A1.2,1.2 0 1 0 4.5,12 Z M10.8,12 A1.2,1.2 0 1 0 13.2,12 A1.2,1.2 0 1 0 10.8,12 Z M17.1,12 A1.2,1.2 0 1 0 19.5,12 A1.2,1.2 0 1 0 17.1,12 Z");
}
