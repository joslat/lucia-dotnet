using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PlatformNavigationTests
{
    [AvaloniaFact]
    public void SystemBackClosesBloomThenPhoneDetailButDoesNotTrapTheRoot()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            state.SelectPlace("garden");
            state.OpenActions();
            Dispatcher.UIThread.RunJobs();
            Assert.True(Back(window));
            Assert.False(state.IsActionsOpen);
            Assert.True(state.IsDetailOpen);
            Assert.True(Back(window));
            Assert.False(state.IsDetailOpen);
            Assert.False(Back(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BackReturnsFromStoryAndLabAndDoesNotDuplicateAfterReattachment()
    {
        var shell = new CompanionShell(new(SampleHome.Create()), null);
        var first = new Window { Width = 390, Height = 844, Content = shell };
        var second = new Window { Width = 844, Height = 390 };
        try
        {
            first.Show();
            Click(shell.FindControl<Button>("StoryButton")!);
            Assert.True(Back(first));
            Assert.IsType<HomeScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            shell.FindControl<Button>("AppearanceButton")!.ContextMenu!.Items.OfType<MenuItem>()
                .Single(item => Equals(item.Header, "Control lab")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            first.Content = null;
            second.Content = shell;
            second.Show();
            Assert.False(Back(first));
            Assert.True(Back(second));
            Assert.IsType<HomeScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Assert.False(Back(second));
        }
        finally { first.Close(); second.Close(); }
    }

    [AvaloniaFact]
    public void BackProtectsEvenAnInvalidUncommittedDraftAndNeverSavesIt()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            Click(shell.FindControl<Button>("BuildHomeButton")!);
            var editor = Assert.IsType<HomeEditorScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            editor.FindControl<TextBox>("PlaceNameInput")!.Text = "";
            Assert.True(Back(window));
            Assert.True(editor.FindControl<Border>("DiscardConfirmation")!.IsVisible);
            Assert.Equal("", editor.FindControl<TextBox>("PlaceNameInput")!.Text);
            NativeCapture.Save(window, "back-discard-phone.png");
            Click(editor.FindControl<Button>("StayInSetup")!);
            Assert.False(editor.FindControl<Border>("DiscardConfirmation")!.IsVisible);
            Assert.True(Back(window));
            Click(editor.FindControl<Button>("DiscardEdits")!);
            Assert.IsType<HomeScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Assert.Equal("Living room", state.Home.Places[0].Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ActivityViewFactoryKeepsOneInMemoryViewWithoutWritingDrafts()
    {
        var app = Assert.IsType<Lucia.Companion.App.App>(Avalonia.Application.Current);
        Assert.Same(app.CreateActivityView(), app.CreateActivityView());
        Assert.IsType<CompanionRoot>(app.CreateActivityView());
    }

    private static bool Back(Window window)
    {
        var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(args);
        return args.Handled;
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
