using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>The explicit offline fixture. Not a projection of a real household.</summary>
public static class SampleHome
{
    public static HomeTopology Create() => new("synthetic-home", "Sample home", WorkbenchFixtures.Places,
        [new("living", "kitchen"), new("living", "porch"), new("porch", "garden"), new("kitchen", "bedroom"), new("kitchen", "utility")]);
}
