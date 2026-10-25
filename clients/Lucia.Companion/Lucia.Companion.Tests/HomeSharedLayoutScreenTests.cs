using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeSharedLayoutScreenTests
{
    [AvaloniaFact]
    public async Task ReadingThenBackPreservesTheEditedDraftAndDoesNotSave()
    {
        var editor = Editor(); HomeTopology? saved = null; editor.SaveRequested += home => saved = home;
        var window = new Window { Width = 390, Height = 844, Content = editor }; window.Show();
        try
        {
            editor.FindControl<TextBox>("HomeNameInput")!.Text = "My unsaved edit";
            Click(editor, "ReviewSharedSetup"); window.UpdateLayout();
            var review = editor.GetVisualDescendants().OfType<HomeSharedLayoutScreen>().Single();
            await review.Controller.PendingOperation; window.UpdateLayout();
            Assert.NotNull(review.Controller.Plan); Assert.Null(saved);
            editor.RequestBack(); window.UpdateLayout();
            Assert.Empty(editor.GetVisualDescendants().OfType<HomeSharedLayoutScreen>());
            Assert.Equal("My unsaved edit", editor.FindControl<TextBox>("HomeNameInput")!.Text);
            Click(editor, "SaveHome"); Assert.Equal("My unsaved edit", saved!.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task AddingAReviewedSharedLayoutStillRequiresSaveAndUsesTheOwnerName()
    {
        var editor = Editor(); HomeTopology? saved = null; editor.SaveRequested += home => saved = home;
        var window = new Window { Width = 1194, Height = 900, Content = editor }; window.Show();
        try
        {
            Click(editor, "ReviewSharedSetup"); window.UpdateLayout();
            var review = editor.GetVisualDescendants().OfType<HomeSharedLayoutScreen>().Single();
            await review.Controller.PendingOperation;
            Click(review, "AddSharedSetupToDraft"); await review.Controller.PendingOperation; window.UpdateLayout();
            Assert.Null(saved); Assert.Empty(editor.GetVisualDescendants().OfType<HomeSharedLayoutScreen>());
            Assert.Equal("Owner home", editor.FindControl<TextBox>("HomeNameInput")!.Text);
            Click(editor, "SaveHome");
            Assert.Equal("Owner home", saved!.Name); Assert.Equal("shared-porch", Assert.Single(saved.Places).Id);
            Assert.Equal("porch", Assert.Single(saved.Bindings).SourcePlaceId);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task InvalidationHidesTheSharedAlternativeButRetainsTheSetupDraft()
    {
        var editor = Editor(); var window = new Window { Width = 390, Height = 844, Content = editor }; window.Show();
        try
        {
            Click(editor, "ReviewSharedSetup"); window.UpdateLayout();
            var review = editor.GetVisualDescendants().OfType<HomeSharedLayoutScreen>().Single();
            await review.Controller.PendingOperation; editor.InvalidateSharedLayout(); window.UpdateLayout();
            Assert.Null(review.Controller.Plan);
            Assert.False(review.FindControl<Border>("SharedAlternative")!.IsVisible);
            Assert.False(review.FindControl<Button>("AddSharedSetupToDraft")!.IsEnabled);
            editor.RequestBack(); Assert.Equal("Personal draft", editor.FindControl<TextBox>("HomeNameInput")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void OfflineEditorDoesNotOfferSharedReadsAndInvalidFieldsCannotBeDiscardedByOpeningReview()
    {
        var offline = new HomeEditorScreen(SharedLayoutAdoptionControllerTests.Draft());
        Assert.False(offline.FindControl<Button>("ReviewSharedSetup")!.IsVisible);
        var editor = Editor(); editor.FindControl<TextBox>("PlaceNameInput")!.Text = " ";
        Click(editor, "ReviewSharedSetup");
        Assert.Empty(editor.GetVisualDescendants().OfType<HomeSharedLayoutScreen>());
        Assert.Equal(" ", editor.FindControl<TextBox>("PlaceNameInput")!.Text);
    }

    [AvaloniaFact]
    public async Task SlowReadDoesNotStealFocusAfterTheUserMovesToAnotherTask()
    {
        var service = SharedLayoutAdoptionControllerTests.Service((_, _) => Task.FromResult<Connection.SharedHomeLayout?>(SharedLayoutAdoptionControllerTests.Layout()));
        using var controller = new SharedLayoutAdoptionController(service, SharedLayoutAdoptionControllerTests.Identity(), SharedLayoutAdoptionControllerTests.Draft);
        using var review = new HomeSharedLayoutScreen(controller);
        var outside = new Button { Content = "Another task" };
        var panel = new DockPanel(); DockPanel.SetDock(outside, Dock.Top); panel.Children.Add(outside); panel.Children.Add(review);
        var window = new Window { Width = 390, Height = 844, Content = panel }; window.Show();
        try
        {
            await controller.PendingOperation; Dispatcher.UIThread.RunJobs();
            var delayed = new TaskCompletionSource<Connection.SharedHomeLayout?>(); service.SharedLayout = (_, _) => delayed.Task;
            review.FindControl<Button>("ReadSharedSetup")!.Focus(); Click(review, "ReadSharedSetup"); outside.Focus();
            delayed.SetResult(SharedLayoutAdoptionControllerTests.Layout()); await controller.PendingOperation;
            Dispatcher.UIThread.RunJobs(); Assert.Equal(outside, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); }
    }

    internal static HomeEditorScreen Editor() => new(SharedLayoutAdoptionControllerTests.Draft(), sharedLayoutFactory: draft =>
        new SharedLayoutAdoptionController(SharedLayoutAdoptionControllerTests.Service((_, _) => Task.FromResult<Connection.SharedHomeLayout?>(SharedLayoutAdoptionControllerTests.Layout())),
            SharedLayoutAdoptionControllerTests.Identity(), draft));
    internal static void Click(Control parent, string name) => parent.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
