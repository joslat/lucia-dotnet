namespace Lucia.Companion.Core;

/// <summary>Known household dashboard sections. Ordering never hides attention or invents new sections.</summary>
public static class HomeSections
{
    public static IReadOnlyList<HomeSection> DefaultOrder { get; } = Array.AsReadOnly(new[]
        { HomeSection.Places, HomeSection.Attention, HomeSection.LuciaNow });

    public static IReadOnlyList<HomeSection> ValidateOrder(IEnumerable<HomeSection> order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var snapshot = order.ToArray();
        if (snapshot.Length != DefaultOrder.Count || !snapshot.ToHashSet().SetEquals(DefaultOrder))
        {
            throw new ArgumentException("Choose each Home section exactly once.", nameof(order));
        }
        return Array.AsReadOnly(snapshot);
    }

    public static string Label(HomeSection section) => section switch
    {
        HomeSection.Places => "Your places",
        HomeSection.Attention => "Needs a look",
        HomeSection.LuciaNow => "Lucia now",
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };
}
