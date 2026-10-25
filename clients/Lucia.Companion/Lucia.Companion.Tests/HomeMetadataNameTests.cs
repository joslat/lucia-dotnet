using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeMetadataNameTests
{
    private static string Label => new string('a', 198) + "\U0001F33F";

    [Fact]
    public void LocalEditingRetainsTheFullSharedNameBudget()
    {
        var editor = new HomeEditorViewModel(SharedLayoutAdoptionControllerTests.Draft());
        editor.SetHomeName(Label); editor.UpdatePlace("local", Label, PlaceIcon.Door);
        Assert.Equal(Label, editor.Build().Name); Assert.Equal(Label, editor.Build().Places[0].Name);
        Assert.Throws<ArgumentException>(() => editor.SetHomeName(new string('a', 201)));
    }

    [Fact]
    public void AuthorizedImportRetainsTheFullNameWithoutTruncation()
    {
        var catalog = new HomeCatalog("home-a", DateTimeOffset.UtcNow, [new("porch", Label, [])]);
        var plan = HomeImportPlan.Create(SharedLayoutAdoptionControllerTests.Draft(), catalog, [new("porch", Label, PlaceIcon.Door)]);
        Assert.Equal(Label, plan.Preview.Places.Single(place => place.Id == "porch").Name);
    }

    [AvaloniaFact]
    public void NativeFieldsAndImportSuggestionMatchTheSharedContract()
    {
        var home = new HomeTopology("home-a", Label, [new("local", Label, PlaceState.Unavailable, "Unknown", [])], []);
        var editor = new HomeEditorScreen(home);
        Assert.Equal(200, editor.FindControl<TextBox>("HomeNameInput")!.MaxLength);
        Assert.Equal(200, editor.FindControl<TextBox>("PlaceNameInput")!.MaxLength);
        Assert.Equal(Label, editor.FindControl<TextBox>("HomeNameInput")!.Text);
        var row = new HomeImportPlaceRow(new("porch", Label, []), null);
        Assert.Equal(200, row.FindControl<TextBox>("ImportPlaceName")!.MaxLength);
        Assert.Equal(Label, row.FindControl<TextBox>("ImportPlaceName")!.Text);
    }
}
