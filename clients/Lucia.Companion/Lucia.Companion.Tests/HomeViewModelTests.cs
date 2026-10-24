using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeViewModelTests
{
    [Fact]
    public void AttentionOrderDoesNotReorderConstellation()
    {
        var home = new HomeTopology("sample", "Sample home",
            [Room("quiet", PlaceState.Quiet), Room("missing", PlaceState.Unavailable), Room("fault", PlaceState.Problem)], []);
        var state = new HomeViewModel(home);
        Assert.Equal(["quiet", "missing", "fault"], state.Home.Places.Select(place => place.Id));
        Assert.Equal(["fault", "missing"], state.AttentionPlaces.Select(place => place.Id));
    }

    [Fact]
    public void SelectionAndActionsShareOnePlace()
    {
        var state = new HomeViewModel(new("sample", "Sample home", [Room("a"), Room("b")], []));
        state.SelectPlace("b");
        state.OpenActions();
        state.TogglePin();
        Assert.Equal("b", state.SelectedPlace!.Id);
        Assert.True(state.IsActionsOpen);
        Assert.True(state.IsPinned("b"));
        state.SelectPlace("a");
        Assert.False(state.IsActionsOpen);
        Assert.False(state.IsPinned("a"));
        Assert.True(state.IsPinned("b"));
    }

    [Fact]
    public void UnknownSelectionIsRejectedWithoutLosingCurrentPlace()
    {
        var state = new HomeViewModel(new("sample", "Sample home", [Room("a")], []));
        state.SelectPlace("a");
        Assert.Throws<ArgumentException>(() => state.SelectPlace("other-home"));
        Assert.Equal("a", state.SelectedPlace!.Id);
    }

    [Fact]
    public void EmptyHomeDoesNotInventSelectionOrActions()
    {
        var state = new HomeViewModel(new("sample", "Sample home", [], []));
        state.OpenActions();
        state.TogglePin();
        Assert.Null(state.SelectedPlace);
        Assert.False(state.IsActionsOpen);
        Assert.Empty(state.AttentionPlaces);
    }

    [Fact]
    public void ClearSelectionDismissesActions()
    {
        var state = new HomeViewModel(new("sample", "Sample home", [Room("a")], []));
        state.SelectPlace("a");
        state.OpenActions();
        state.ClearSelection();
        Assert.Null(state.SelectedPlace);
        Assert.False(state.IsActionsOpen);
    }

    private static Place Room(string id, PlaceState state = PlaceState.Quiet) => new(id, id, state, "Sample", []);
}
