using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Retains unambiguous reading controls only within the same place and exact source-binding scope.</summary>
public sealed class ObservationRows
{
    private static readonly ConditionalWeakTable<StackPanel, ObservationRows> Instances = new();
    private string? placeId;
    private ObservationBinding[] bindings = [];

    public static void Update(StackPanel panel, Place place, HomeTopology home) => Instances.GetValue(panel, _ => new()).Apply(panel, place, home);

    private void Apply(StackPanel panel, Place place, HomeTopology home)
    {
        var nextBindings = home.Bindings.Where(item => item.PlaceId == place.Id).ToArray();
        var sameScope = placeId == place.Id && bindings.SequenceEqual(nextBindings);
        var previous = panel.Children.OfType<ObservationRow>().Where(row => row.Reading is not null)
            .GroupBy(row => (row.Reading!.Label, row.Reading.Role)).Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        var counts = place.Observations.GroupBy(item => (item.Label, item.Role)).ToDictionary(group => group.Key, group => group.Count());
        var next = place.Observations.Select(reading =>
        {
            if (sameScope && counts[(reading.Label, reading.Role)] == 1 && previous.TryGetValue((reading.Label, reading.Role), out var row))
            { row.Update(reading); return row; }
            return new ObservationRow(reading);
        }).ToArray();

        foreach (var obsolete in panel.Children.Where(child => !next.Contains(child)).ToArray()) { panel.Children.Remove(obsolete); }
        for (var index = 0; index < next.Length; index++)
        {
            var currentIndex = panel.Children.IndexOf(next[index]);
            if (currentIndex < 0) { panel.Children.Insert(index, next[index]); }
            else if (currentIndex != index) { panel.Children.Move(currentIndex, index); }
        }
        placeId = place.Id;
        bindings = nextBindings;
    }
}
