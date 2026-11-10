using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>The explicit offline fixture. Not a projection of a real household.</summary>
public static class SampleHome
{
    /// <summary>The fixed synthetic time the sample readings describe.</summary>
    public static DateTimeOffset CapturedAt { get; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static HomeTopology Create() => new("synthetic-home", "Sample home", WorkbenchFixtures.Places,
        [new("living", "kitchen"), new("living", "porch"), new("porch", "garden"), new("kitchen", "bedroom"), new("kitchen", "utility")]);
}
