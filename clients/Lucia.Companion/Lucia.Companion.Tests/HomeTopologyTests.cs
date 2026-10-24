using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeTopologyTests
{
    [Fact]
    public void CyclesAndDisconnectedPlacesAreValid()
    {
        var home = new HomeTopology("sample", "Sample home", [Room("a"), Room("b"), Room("c"), Room("d")],
            [new("a", "b"), new("b", "c"), new("c", "a")]);
        Assert.Equal(4, home.Places.Count);
        Assert.Equal(3, home.Connections.Count);
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("a", "outside-this-home")]
    public void InvalidEdgeIsRejected(string from, string to) =>
        Assert.Throws<ArgumentException>(() => new HomeTopology("sample", "Sample home", [Room("a"), Room("b")], [new(from, to)]));

    [Fact]
    public void ReversedDuplicateEdgeIsRejected() =>
        Assert.Throws<ArgumentException>(() => new HomeTopology("sample", "Sample home", [Room("a"), Room("b")], [new("a", "b"), new("b", "a")]));

    [Fact]
    public void DuplicatePlaceIdsAreRejected() =>
        Assert.Throws<ArgumentException>(() => new HomeTopology("sample", "Sample home", [Room("a"), Room("a")], []));

    [Fact]
    public void InputListCannotChangeValidatedTopology()
    {
        var places = new List<Place> { Room("a") };
        var home = new HomeTopology("sample", "Sample home", places, []);
        places.Add(Room("b"));
        Assert.Single(home.Places);
    }

    [Fact]
    public void EmptyHomeIsValid()
    {
        var home = new HomeTopology("sample", "Sample home", [], []);
        Assert.Empty(home.Places);
    }

    private static Place Room(string id) => new(id, id, PlaceState.Quiet, "No sample change", []);
}
