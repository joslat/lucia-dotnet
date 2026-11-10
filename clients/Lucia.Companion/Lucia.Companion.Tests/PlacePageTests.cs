using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PlacePageTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    public void PhonePageShowsHeroExplanationReadingsAndSampleHistory(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            state.SelectPlace("bedroom");
            Dispatcher.UIThread.RunJobs();
            var page = home.FindControl<PlacePage>("PlacePage")!;
            Assert.True(page.IsEffectivelyVisible);
            Assert.Equal("Sample home · Inside", page.FindControl<TextBlock>("Breadcrumb")!.Text);
            Assert.Equal("Bedroom", page.FindControl<TextBlock>("PageTitle")!.Text);
            Assert.Equal("Data unavailable", page.FindControl<TextBlock>("StatusChipText")!.Text);
            Assert.Equal("The sample source is unavailable; the old value is not current.", page.FindControl<TextBlock>("ExplanationTitle")!.Text);
            Assert.Equal("Its last reading was 19.2 °C at 10:00 UTC. The current value isn't known, so it is shown as unavailable, not as normal.",
                page.FindControl<TextBlock>("ExplanationBody")!.Text);
            Assert.Equal("Bedroom now", page.FindControl<TextBlock>("ReadingsTitle")!.Text);
            var row = Assert.Single(page.FindControl<StackPanel>("PageObservations")!.Children.OfType<ObservationRow>());
            Assert.True(row.FindControl<Control>("ReadingIcon")!.IsVisible);

            var chart = page.FindControl<ReadingChart>("TodayChart")!;
            Assert.True(chart.IsEffectivelyVisible);
            Assert.Equal("Temperature today", page.FindControl<TextBlock>("ChartTitle")!.Text);
            Assert.True(chart.HasGap);
            Assert.Equal(new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero), chart.Points[^1].At);
            Assert.Contains("Sample", chart.GetValue(AutomationProperties.NameProperty), StringComparison.Ordinal);
            Assert.False(page.FindControl<TextBlock>("HistoryNote")!.IsVisible);
            NativeCapture.Save(window, $"place-page-{width}-{(dark ? "dark" : "light")}.png");

            home.FindControl<ScrollViewer>("ContextScroll")!.Offset = new Vector(0, 900);
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"place-page-lower-{width}-{(dark ? "dark" : "light")}.png");

            var pin = page.FindControl<ToggleButton>("PinToggle")!;
            Assert.False(pin.IsChecked);
            pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(state.IsPinned("bedroom"));
            Dispatcher.UIThread.RunJobs();
            Assert.True(pin.IsChecked);

            page.FindControl<Button>("BackToHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(state.IsDetailOpen);
            Assert.False(page.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WhyShownExplainsTheAttentionRule()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            state.SelectPlace("utility");
            Dispatcher.UIThread.RunJobs();
            var page = home.FindControl<PlacePage>("PlacePage")!;
            var why = page.FindControl<TextBlock>("WhyText")!;
            Assert.False(why.IsVisible);
            page.FindControl<ToggleButton>("WhyShown")!.IsChecked = true;
            Assert.True(why.IsVisible);
            Assert.Equal("Shown because a configured source reports a problem. It is a report, not a confirmed fault.", why.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ConnectedPagesNeverDrawSampleHistory()
    {
        var setup = new HomeTopology("home", "Our home",
            [new("hall", "Hall", PlaceState.Quiet, "Recent source reports.", [new("Hall temperature · Temperature", "20 °C", ObservationQuality.Current, DateTimeOffset.UtcNow) { Role = ObservationRole.Temperature }])], []);
        var state = new HomeViewModel(setup, isDemo: false);
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            state.SelectPlace("hall");
            Dispatcher.UIThread.RunJobs();
            var page = home.FindControl<PlacePage>("PlacePage")!;
            Assert.False(page.FindControl<Control>("ChartCard")!.IsVisible);
            Assert.False(page.FindControl<Control>("TimelineCard")!.IsVisible);
            Assert.True(page.FindControl<TextBlock>("HistoryNote")!.IsVisible);
            Assert.Equal("Our home", page.FindControl<TextBlock>("Breadcrumb")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TabletSidePaneOpensTheFullPageAndBackReturnsToIt()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 1194, Height = 834, Content = shell };
        try
        {
            window.Show();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            state.SelectPlace("garden");
            Dispatcher.UIThread.RunJobs();
            var page = home.FindControl<PlacePage>("PlacePage")!;
            Assert.False(home.IsPageVisible);
            var side = home.FindControl<PlaceDetail>("DesktopDetail")!;
            Assert.Equal("Changed · Gate open", side.FindControl<TextBlock>("StatusLine")!.Text);
            side.FindControl<Button>("OpenPlacePage")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(home.IsPageVisible);
            Assert.True(home.FindControl<ScrollViewer>("DetailScroll")!.IsVisible);
            Assert.False(home.FindControl<ScrollViewer>("HomeScroll")!.IsVisible);
            NativeCapture.Save(window, "place-page-1194.png");
            window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(home.IsPageVisible);
            Assert.True(home.FindControl<ScrollViewer>("HomeScroll")!.IsVisible);
            Assert.Equal("garden", state.SelectedPlace?.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TheTimelineLinksToTheWholeHomeStory()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            state.SelectPlace("garden");
            Dispatcher.UIThread.RunJobs();
            var page = home.FindControl<PlacePage>("PlacePage")!;
            var entries = page.FindControl<StackPanel>("TimelineEntries")!.Children.OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToArray();
            // Today's sample day stops at the sample clock (14:00 in Zurich), so only the midday gate report is in it.
            Assert.Equal(["12:20–12:26 · Gate reported open"], entries);
            Assert.Contains("Saturday 24 October", page.FindControl<TextBlock>("TimelineCaption")!.Text, StringComparison.Ordinal);
            page.FindControl<Button>("OpenStory")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            // "Whole home story" opens the whole day; Bloom's History is the place-filtered path.
            Assert.Null(story.State.SelectedPlaceId);
            Assert.Equal(ShellDestination.Story, shell.Navigation.Selected);
        }
        finally { window.Close(); }
    }
}
