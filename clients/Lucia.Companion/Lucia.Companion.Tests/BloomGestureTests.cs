using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class BloomGestureTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void BloomOffersOpenHistoryPinAndCloseAsNamedButtons(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var (shell, state, home, window) = Open(width, height);
        try
        {
            state.SelectPlace("porch");
            state.CloseDetail();
            state.OpenActions();
            Dispatcher.UIThread.RunJobs();
            var bloom = home.FindControl<BloomActions>("PlaceActions")!;
            Assert.True(home.FindControl<Popup>("ActionPopup")!.IsOpen);
            Assert.Equal("Open Porch", bloom.FindControl<Button>("OpenButton")!.GetValue(AutomationProperties.NameProperty));
            Assert.Equal("Porch history in Story", bloom.FindControl<Button>("HistoryButton")!.GetValue(AutomationProperties.NameProperty));
            Assert.Null(bloom.FindControl<Button>("AskLuciaButton"));
            NativeCapture.Save(window, $"bloom-{width}-{(dark ? "dark" : "light")}.png");

            bloom.FindControl<Button>("OpenButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(state.IsActionsOpen);
            Assert.True(home.IsPageVisible);
            Assert.Equal("porch", state.SelectedPlace?.Id);

            state.CloseDetail();
            state.OpenActions();
            Dispatcher.UIThread.RunJobs();
            bloom.FindControl<Button>("HistoryButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Assert.Equal("porch", story.State.SelectedPlaceId);
            Assert.False(state.IsActionsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HoldSlideAndReleaseOnAnActionInvokesIt()
    {
        var (shell, state, home, window) = Open(390, 844);
        try
        {
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            constellation.HoldDelay = TimeSpan.Zero;
            var porch = constellation.Orbs.Single(orb => (string)orb.Tag! == "porch");
            window.MouseDown(Center(porch, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(state.IsActionsOpen);
            Assert.Equal("porch", state.SelectedPlace?.Id);
            Assert.False(state.IsDetailOpen);
            var bloom = home.FindControl<BloomActions>("PlaceActions")!;
            var history = bloom.FindControl<Button>("HistoryButton")!;
            window.MouseMove(Center(history, window));
            Assert.Contains("armed", history.Classes);
            NativeCapture.Save(window, "bloom-armed-390.png");
            window.MouseUp(Center(history, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(state.IsActionsOpen);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Assert.Equal("porch", story.State.SelectedPlaceId);
            Assert.DoesNotContain("armed", history.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SlidingOutOfTheBloomCancelsAndReleasingOnThePlaceKeepsItOpen()
    {
        var (shell, state, home, window) = Open(390, 844);
        try
        {
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            constellation.HoldDelay = TimeSpan.Zero;
            var porch = constellation.Orbs.Single(orb => (string)orb.Tag! == "porch");
            window.MouseDown(Center(porch, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(new Point(4, 4));
            window.MouseUp(new Point(4, 4), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(state.IsActionsOpen);
            Assert.False(state.IsDetailOpen);
            Assert.IsType<HomeScreen>(shell.CurrentScreen);
            Assert.False(state.IsPinned("porch"));

            window.MouseDown(Center(porch, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.MouseUp(Center(porch, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(state.IsActionsOpen);
            Assert.False(state.IsDetailOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AQuickTapStillOpensThePlaceWithoutBloom()
    {
        var (_, state, home, window) = Open(390, 844);
        try
        {
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            var garden = constellation.Orbs.Single(orb => (string)orb.Tag! == "garden");
            window.MouseDown(Center(garden, window), MouseButton.Left);
            window.MouseUp(Center(garden, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(state.IsActionsOpen);
            Assert.Equal("garden", state.SelectedPlace?.Id);
            Assert.True(state.IsDetailOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LargeTextListsTheSameActions()
    {
        var (_, state, home, window) = Open(390, 844);
        try
        {
            CompanionTypography.Apply(size => size * 2);
            state.SelectPlace("garden");
            state.CloseDetail();
            state.OpenActions();
            Dispatcher.UIThread.RunJobs();
            var bloom = home.FindControl<BloomActions>("PlaceActions")!;
            Assert.True(bloom.FindControl<Border>("LargeTextActions")!.IsVisible);
            Assert.Equal(["Open", "History", "Pin place", "Close"],
                new[] { "LargeOpen", "LargeHistory", "LargePin", "LargeClose" }.Select(name => bloom.FindControl<Button>(name)!.Content as string));
            bloom.FindControl<Button>("LargeOpen")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(home.IsPageVisible);
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    private static (CompanionShell Shell, HomeViewModel State, HomeScreen Home, Window Window) Open(int width, int height)
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (shell, state, Assert.IsType<HomeScreen>(shell.CurrentScreen), window);
    }

    private static Point Center(Control control, Window window)
    {
        var target = control is PlaceOrb orb ? orb.GetVisualDescendants().OfType<Control>().First(item => item.Name == "Orb") : control;
        return target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
    }
}
