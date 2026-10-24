using Avalonia;

namespace Lucia.Companion.App;

/// <summary>Maps the existing type roles through the platform's text-unit conversion, not a global UI zoom.</summary>
public static class CompanionTypography
{
    private static readonly int[] Sizes = [12, 13, 14, 16, 24, 32, 34];

    public static void Apply(Func<double, double> mapSize)
    {
        ArgumentNullException.ThrowIfNull(mapSize);
        var mapped = Sizes.Select(size => (Base: size, Value: mapSize(size))).ToArray();
        if (mapped.Any(size => !double.IsFinite(size.Value) || size.Value <= 0))
        {
            throw new ArgumentException("Platform text sizes must be positive and finite.", nameof(mapSize));
        }
        var resources = Application.Current?.Resources ?? throw new InvalidOperationException("Initialize the application first.");
        foreach (var size in mapped) { resources[$"TextSize{size.Base}"] = size.Value; }
    }
}
