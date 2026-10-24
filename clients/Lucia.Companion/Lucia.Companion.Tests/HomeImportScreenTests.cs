using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeImportScreenTests
{
    [AvaloniaFact]
    public void CompactPreviewReplacesTheLongSelectionAndSystemBackReturnsWithoutLosingChoices()
    {
        var editor = new HomeEditorScreen(new("home-a", "Our home", [], []), [], Catalog());
        var window = new Window { Width = 390, Height = 844, Content = editor };
        window.Show();
        try
        {
            Click(editor, "ImportPlaces"); window.UpdateLayout();
            var import = editor.GetVisualDescendants().OfType<HomeImportScreen>().Single();
            var choice = import.GetVisualDescendants().OfType<HomeImportPlaceRow>().Single().FindControl<CheckBox>("SelectImportPlace")!;
            choice.IsChecked = true; Click(import, "PreviewImport"); window.UpdateLayout();
            Assert.False(import.FindControl<StackPanel>("ImportSelection")!.IsVisible);
            Assert.True(import.FindControl<StackPanel>("ImportReview")!.IsVisible);
            editor.RequestBack(); window.UpdateLayout();
            Assert.Same(import, editor.GetVisualDescendants().OfType<HomeImportScreen>().Single());
            Assert.True(import.FindControl<StackPanel>("ImportSelection")!.IsVisible);
            Assert.True(choice.IsChecked);
            Assert.False(import.FindControl<Button>("AddImportToDraft")!.IsEnabled);
            editor.RequestBack(); window.UpdateLayout();
            Assert.Empty(editor.GetVisualDescendants().OfType<HomeImportScreen>());
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void ImportRequiresSelectionThenReviewAndInputChangesInvalidateThatReview()
    {
        var original = new HomeTopology("home-a", "Our home", [], []);
        var screen = new HomeImportScreen(original, Catalog());
        var window = new Window { Width = 390, Height = 844, Content = screen };
        window.Show();
        try
        {
            Assert.False(screen.FindControl<Button>("AddImportToDraft")!.IsEnabled);
            var row = screen.GetVisualDescendants().OfType<HomeImportPlaceRow>().Single();
            row.FindControl<CheckBox>("SelectImportPlace")!.IsChecked = true;
            Click(screen, "PreviewImport");
            Assert.True(screen.FindControl<Button>("AddImportToDraft")!.IsEnabled);
            row.FindControl<TextBox>("ImportPlaceName")!.Text = "Front step";
            Assert.False(screen.FindControl<Button>("AddImportToDraft")!.IsEnabled);
            Click(screen, "PreviewImport");
            Core.HomeTopology? accepted = null;
            screen.ApplyRequested += plan => accepted = plan.ApplyTo(original);
            Click(screen, "AddImportToDraft");
            Assert.Equal("Front step", Assert.Single(accepted!.Places).Name);
            Assert.Equal("porch", Assert.Single(accepted.Bindings).SourcePlaceId);
            Assert.Empty(original.Places);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BackFromImportReturnsToTheSameEditorAndAddingStillRequiresTheNormalSave()
    {
        var original = new HomeTopology("home-a", "Our home", [], []);
        var editor = new HomeEditorScreen(original, [], Catalog());
        HomeTopology? saved = null;
        editor.SaveRequested += home => saved = home;
        var window = new Window { Width = 1000, Height = 900, Content = editor };
        window.Show();
        try
        {
            Click(editor, "ImportPlaces");
            window.UpdateLayout();
            Assert.Single(editor.GetVisualDescendants().OfType<HomeImportScreen>());
            editor.RequestBack();
            window.UpdateLayout();
            Assert.Empty(editor.GetVisualDescendants().OfType<HomeImportScreen>());
            Assert.Null(saved);
            Click(editor, "ImportPlaces");
            window.UpdateLayout();
            var import = editor.GetVisualDescendants().OfType<HomeImportScreen>().Single();
            import.GetVisualDescendants().OfType<HomeImportPlaceRow>().Single().FindControl<CheckBox>("SelectImportPlace")!.IsChecked = true;
            Click(import, "PreviewImport");
            Click(import, "AddImportToDraft");
            Assert.Null(saved);
            Assert.Empty(original.Places);
            Click(editor, "SaveHome");
            Assert.Equal("porch", Assert.Single(saved!.Places).Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void UnavailableCatalogIsNotInventedAndCancelNeverAppliesAPreview()
    {
        var original = new HomeTopology("home-a", "Our home", [], []);
        var editor = new HomeEditorScreen(original);
        var window = new Window { Width = 390, Height = 844, Content = editor };
        window.Show();
        try { Assert.False(editor.FindControl<Button>("ImportPlaces")!.IsVisible); }
        finally { window.Close(); }
        var screen = new HomeImportScreen(original, Catalog());
        var cancelled = false;
        var applied = false;
        screen.CancelRequested += () => cancelled = true;
        screen.ApplyRequested += _ => applied = true;
        Click(screen, "CancelImport");
        Assert.True(cancelled);
        Assert.False(applied);
    }

    private static HomeCatalog Catalog() => new("home-a", DateTimeOffset.UtcNow,
        [new("porch", "Porch", [new("sensor.temperature", "Porch temperature", ObservationRole.Temperature)])]);
    private static void Click(Control parent, string name) => parent.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
