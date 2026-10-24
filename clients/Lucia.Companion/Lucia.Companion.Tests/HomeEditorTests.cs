using Lucia.Companion.App;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeEditorTests
{
    [Fact]
    public void DraftDoesNotChangeOriginalAndNewPlaceHasNoInventedReadings()
    {
        var original = SampleHome.Create();
        var draft = new HomeEditorViewModel(original);
        var addedId = draft.AddPlace("Greenhouse", PlaceIcon.Garden);
        draft.SetConnections(addedId, ["garden"]);
        var result = draft.Build();
        Assert.Equal(6, original.Places.Count);
        var added = Assert.Single(result.Places, place => place.Id == addedId);
        Assert.Empty(added.Observations);
        Assert.Equal(PlaceState.Unavailable, added.State);
        Assert.Contains(result.Connections, edge => edge.FromPlaceId == addedId && edge.ToPlaceId == "garden");
    }

    [Fact]
    public void RemovePlaceRemovesOnlyItsConnections()
    {
        var draft = new HomeEditorViewModel(SampleHome.Create());
        draft.RemovePlace("kitchen");
        var result = draft.Build();
        Assert.DoesNotContain(result.Connections, edge => edge.FromPlaceId == "kitchen" || edge.ToPlaceId == "kitchen");
        Assert.Contains(result.Connections, edge => edge.FromPlaceId == "porch" && edge.ToPlaceId == "garden");
    }

    [Fact]
    public void InvalidConnectionEditIsAtomic()
    {
        var draft = new HomeEditorViewModel(SampleHome.Create());
        var before = draft.Build().Connections.ToArray();
        Assert.Throws<ArgumentException>(() => draft.SetConnections("garden", ["garden"]));
        Assert.Equal(before, draft.Build().Connections);
    }

    [Fact]
    public void RenameAndMovePreserveStableIdentityAndReadings()
    {
        var draft = new HomeEditorViewModel(SampleHome.Create());
        draft.UpdatePlace("garden", "Back garden", PlaceIcon.Garden);
        draft.MovePlace("garden", -1);
        var result = draft.Build();
        Assert.Equal("garden", result.Places[0].Id);
        Assert.Equal("Back garden", result.Places[0].Name);
        Assert.NotEmpty(result.Places[0].Observations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankRoomNameIsRejected(string name)
    {
        var draft = new HomeEditorViewModel(SampleHome.Create());
        Assert.Throws<ArgumentException>(() => draft.UpdatePlace("garden", name, PlaceIcon.Garden));
    }
}
