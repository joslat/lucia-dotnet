using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class AccessibleFeedbackTests
{
    [AvaloniaFact]
    public void AHomeNameErrorSurvivesASuccessfulEditToAnotherPlace()
    {
        var editor = new HomeEditorScreen();
        var window = new Window { Width = 1194, Height = 800, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var homeName = editor.FindControl<TextBox>("HomeNameInput")!;
            homeName.Text = " ";
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.FindControl<Button>("AddPlace")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("New place", editor.FindControl<TextBox>("PlaceNameInput")!.Text);
            Assert.True(editor.FindControl<TextBlock>("HomeErrorText")!.IsVisible);
            Assert.Contains("Enter a home name.", ControlAutomationPeer.CreatePeerForElement(homeName)!.GetHelpText());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void APlaceNameErrorSurvivesAnUnrelatedSuccessfulBindingChange()
    {
        var source = new ObservationSource("sensor.synthetic_humidity", "Synthetic humidity", [ObservationRole.Humidity]);
        var editor = new HomeEditorScreen(SampleHome.Create(), [source]);
        var window = new Window { Width = 1194, Height = 800, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var name = editor.FindControl<TextBox>("PlaceNameInput")!;
            name.Text = " ";
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var picker = editor.FindControl<SourceBindingPicker>("BindingPicker")!;
            picker.FindControl<ComboBox>("BindingSource")!.SelectedIndex = 0;
            picker.FindControl<ComboBox>("BindingRole")!.SelectedIndex = 0;
            picker.FindControl<Button>("AddBinding")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Single(picker.FindControl<StackPanel>("BindingRows")!.Children);
            Assert.True(editor.FindControl<TextBlock>("ErrorText")!.IsVisible);
            Assert.Contains("Enter a place name.", ControlAutomationPeer.CreatePeerForElement(name)!.GetHelpText());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownDurationWorkHasANamedStatusWithoutARangeInItsSemanticTree(bool reduced)
    {
        MotionSettings.Current.ReduceMotion = reduced;
        var progress = new OperationProgress { IsBusy = true };
        var sibling = new TextBlock { Text = "Saving your setup" };
        var content = new StackPanel { Children = { progress, sibling } };
        var window = new Window { Width = 390, Height = 160, Content = content };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var peer = ControlAutomationPeer.CreatePeerForElement(progress)!;
            Assert.Contains("Operation in progress", peer.GetName());
            Assert.DoesNotContain(Tree(peer), child => child is IRangeValueProvider);
            Assert.Contains(Tree(ControlAutomationPeer.CreatePeerForElement(content)!), child => child.GetName() == sibling.Text);
            progress.IsBusy = false;
            Assert.False(progress.IsEffectivelyVisible);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("PlaceNameInput", "ErrorText")]
    [InlineData("HomeNameInput", "HomeErrorText")]
    public void AnInvalidNameExposesItsExplanationOnTheFieldAndClearsItAfterCorrection(string fieldId, string errorId)
    {
        var editor = new HomeEditorScreen();
        var window = new Window { Width = 390, Height = 800, Content = editor };
        HomeTopology? saved = null;
        editor.SaveRequested += home => saved = home;
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var field = editor.FindControl<TextBox>(fieldId)!;
            var peer = ControlAutomationPeer.CreatePeerForElement(field)!;
            var normalHelp = peer.GetHelpText();
            Assert.Contains(fieldId == "PlaceNameInput" ? "this place" : "this home", normalHelp);
            field.Text = " ";
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var error = editor.FindControl<TextBlock>(errorId)!;
            Assert.True(error.IsVisible);
            Assert.Null(saved);
            Assert.Contains(error.Text!, peer.GetHelpText());
            Assert.Contains(fieldId == "PlaceNameInput" ? "Place name" : "Home name", peer.GetHelpText());
            field.Text = fieldId == "PlaceNameInput" ? "Sitting room" : "Our home";
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(saved);
            Assert.False(error.IsVisible);
            Assert.Equal(normalHelp, peer.GetHelpText());
        }
        finally { window.Close(); }
    }

    private static IEnumerable<AutomationPeer> Tree(AutomationPeer peer)
    {
        yield return peer;
        foreach (var child in peer.GetChildren() ?? [])
        { foreach (var descendant in Tree(child)) { yield return descendant; } }
    }
}
