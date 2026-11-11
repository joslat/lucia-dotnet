using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

public sealed class ShellNavigationTests
{
    private static readonly ShellDestination[] Order = [ShellDestination.Home, ShellDestination.Story, ShellDestination.Lucia, ShellDestination.More];

    [AvaloniaTheory]
    [InlineData(ShellDestination.Home)]
    [InlineData(ShellDestination.Story)]
    [InlineData(ShellDestination.Lucia)]
    [InlineData(ShellDestination.More)]
    public void TheCurrentDestinationIsIdentifiedInItsPortableAccessibleName(ShellDestination selected)
    {
        var navigation = new ShellNavigation();
        navigation.Select(selected);
        foreach (var destination in Order)
        {
            var button = navigation.ButtonFor(destination);
            var peer = ControlAutomationPeer.CreatePeerForElement(button)!;
            Assert.Equal(destination == selected ? $"{destination}, current page" : destination.ToString(), peer.GetName());
            Assert.Equal(destination.ToString(), ((StackPanel)button.Content!).Children.OfType<TextBlock>().Single().Text);
        }
    }

    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void PhoneUsesABottomBarAndTabletALeftRailWithTheSameFourDestinations(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var navigation = shell.Navigation;
            Assert.Equal(width >= 600, navigation.IsRail);
            Assert.Equal(Order, navigation.Destinations);
            Assert.Equal(["Home", "Story", "Lucia", "More"], Order.Select(item => navigation.ButtonFor(item).Tag));
            Assert.Equal(ShellDestination.Home, navigation.Selected);
            Assert.Contains("selected", navigation.ButtonFor(ShellDestination.Home).Classes);
            Assert.All(Order.Skip(1), item => Assert.DoesNotContain("selected", navigation.ButtonFor(item).Classes));
            var origin = navigation.TranslatePoint(default, window)!.Value;
            if (navigation.IsRail)
            {
                Assert.Equal(0, origin.X);
                Assert.Equal(height, navigation.Bounds.Height, 1);
                Assert.True(navigation.Bounds.Width <= 96);
                Assert.True(navigation.FindControl<Control>("LuciaMark")!.IsEffectivelyVisible);
                var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
                Assert.True(home.TranslatePoint(default, window)!.Value.X >= navigation.Bounds.Width);
            }
            else
            {
                Assert.Equal(height, origin.Y + navigation.Bounds.Height, 1);
                Assert.Equal(width, navigation.Bounds.Width, 1);
                Assert.False(navigation.FindControl<Control>("LuciaMark")!.IsEffectivelyVisible);
            }
            Assert.All(Order, item => Assert.True(navigation.ButtonFor(item).Bounds.Height >= 48));
            // The old top Options, Setup and Story buttons are replaced by the four destinations.
            Assert.Null(shell.FindControl<Button>("AppearanceButton"));
            Assert.Null(shell.FindControl<Button>("BuildHomeButton"));
            Assert.Null(shell.FindControl<Button>("StoryButton"));
            NativeCapture.Save(window, $"nav-home-{width}-{(dark ? "dark" : "light")}.png");
            foreach (var destination in Order.Skip(1))
            {
                Click(navigation.ButtonFor(destination));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(destination, navigation.Selected);
                NativeCapture.Save(window, $"nav-{destination.ToString().ToLowerInvariant()}-{width}-{(dark ? "dark" : "light")}.png");
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DemoDestinationsShowSampleStoryActivityAndMoreWithControlLab()
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            Navigate(shell, ShellDestination.Story);
            Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Navigate(shell, ShellDestination.Lucia);
            var lucia = Assert.IsType<LuciaScreen>(shell.CurrentScreen);
            Assert.True(lucia.FindControl<LuciaNow>("DemoActivity")!.IsVisible);
            Assert.False(lucia.FindControl<ConnectedLuciaNow>("ConnectedActivity")!.IsVisible);
            Assert.Contains(lucia.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text?.Contains("Synthetic", StringComparison.Ordinal) == true);
            var more = OpenMore(shell);
            Assert.True(more.FindControl<Button>("OpenControlLab")!.IsVisible);
            Assert.False(more.FindControl<Button>("RefreshHomeReports")!.IsVisible);
            Click(more.FindControl<Button>("OpenControlLab")!);
            Assert.IsType<ControlWorkbench>(shell.CurrentScreen);
            Assert.Equal(ShellDestination.More, shell.Navigation.Selected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ConnectedStorySaysHistoryIsNotConnectedAndMoreHidesTheLab()
    {
        var state = new HomeViewModel(new("connected-home", "Our home", [], []), isDemo: false);
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        var refreshes = 0;
        shell.RefreshRequested += () => refreshes++;
        try
        {
            window.Show();
            Navigate(shell, ShellDestination.Story);
            var pending = Assert.IsType<HistoryPendingScreen>(shell.CurrentScreen);
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, "nav-story-connected-390.png");
            Assert.True(shell.Navigation.ButtonFor(ShellDestination.Story).IsVisible);
            Assert.Contains(pending.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "History isn't connected yet");
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<StoryScreen>(), _ => true);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text?.Contains("Sample", StringComparison.OrdinalIgnoreCase) == true);
            Navigate(shell, ShellDestination.Lucia);
            var lucia = Assert.IsType<LuciaScreen>(shell.CurrentScreen);
            Assert.False(lucia.FindControl<LuciaNow>("DemoActivity")!.IsVisible);
            Assert.True(lucia.FindControl<ConnectedLuciaNow>("ConnectedActivity")!.IsVisible);
            var more = OpenMore(shell);
            Assert.False(more.FindControl<Button>("OpenControlLab")!.IsEffectivelyVisible);
            Assert.True(more.FindControl<Button>("RefreshHomeReports")!.IsVisible);
            Click(more.FindControl<Button>("RefreshHomeReports")!);
            Assert.Equal(1, refreshes);
            shell.SetRefreshing(true);
            Assert.False(more.FindControl<Button>("RefreshHomeReports")!.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MoreHoldsAppearanceCustomizeSetupAndConnection()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        var connectionRequests = 0;
        shell.ConnectionRequested += () => connectionRequests++;
        try
        {
            window.Show();
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
            var more = OpenMore(shell);
            Assert.True(more.FindControl<RadioButton>("ThemeSystem")!.IsChecked);
            more.FindControl<RadioButton>("ThemeDark")!.IsChecked = true;
            Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
            more.FindControl<RadioButton>("ThemeLight")!.IsChecked = true;
            Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
            Click(more.FindControl<Button>("OpenConnection")!);
            Assert.Equal(1, connectionRequests);

            Click(more.FindControl<Button>("CustomizeHome")!);
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            Assert.True(home.FindControl<Expander>("CustomizeHome")!.IsExpanded);
            Assert.Equal(ShellDestination.Home, shell.Navigation.Selected);

            more = OpenMore(shell);
            Click(more.FindControl<Button>("OpenHomeSetup")!);
            var editor = Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
            Assert.False(shell.Navigation.IsVisible);
            Click(editor.FindControl<Button>("CancelEdit")!);
            Assert.IsType<HomeScreen>(shell.CurrentScreen);
            Assert.True(shell.Navigation.IsVisible);
            Assert.Equal(ShellDestination.Home, shell.Navigation.Selected);
        }
        finally { window.Close(); Application.Current!.RequestedThemeVariant = ThemeVariant.Default; }
    }

    [AvaloniaFact]
    public void BackReturnsFromEachDestinationToHomeAndFromTheLabToMore()
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 1194, Height = 834, Content = shell };
        try
        {
            window.Show();
            foreach (var destination in Order.Skip(1))
            {
                Navigate(shell, destination);
                Assert.True(Back(window));
                Assert.IsType<HomeScreen>(shell.CurrentScreen);
                Assert.Equal(ShellDestination.Home, shell.Navigation.Selected);
            }
            Click(OpenMore(shell).FindControl<Button>("OpenControlLab")!);
            Assert.True(Back(window));
            Assert.IsType<MoreScreen>(shell.CurrentScreen);
            Assert.True(Back(window));
            Assert.IsType<HomeScreen>(shell.CurrentScreen);
            Assert.False(Back(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReselectingStoryKeepsTheSameDayAndSelection()
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            story.State.SelectChapter(story.State.VisibleChapters.Last().Id);
            Navigate(shell, ShellDestination.Home);
            Navigate(shell, ShellDestination.Story);
            Assert.Same(story, shell.CurrentScreen);
            Assert.Equal(story.State.VisibleChapters.Last().Id, story.State.SelectedChapterId);
        }
        finally { window.Close(); }
    }

    internal static void Navigate(CompanionShell shell, ShellDestination destination)
    {
        Click(shell.Navigation.ButtonFor(destination));
        Dispatcher.UIThread.RunJobs();
    }

    internal static MoreScreen OpenMore(CompanionShell shell)
    {
        Navigate(shell, ShellDestination.More);
        return Assert.IsType<MoreScreen>(shell.CurrentScreen);
    }

    /// <summary>Opens Home setup the way a person does: More, then Home setup.</summary>
    internal static void OpenSetup(CompanionShell shell)
    {
        Click(OpenMore(shell).FindControl<Button>("OpenHomeSetup")!);
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Back(Window window)
    {
        var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(args);
        return args.Handled;
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
