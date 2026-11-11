using Avalonia;
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
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class TodaySoFarTests
{
    [AvaloniaTheory]
    [InlineData(300, false)]
    [InlineData(300, true)]
    [InlineData(358, false)]
    [InlineData(358, true)]
    public void TheStoryActionLeavesWholeWordsReadableInTheHeading(int width, bool large)
    {
        CompanionTypography.Apply(size => size * (large ? 2 : 1));
        var today = new TodaySoFar();
        var window = new Window { Width = width, Height = 420, Content = today };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var heading = today.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Today so far");
            var words = heading.Text!;
            foreach (var line in heading.TextLayout.TextLines.SkipLast(1))
            {
                var end = line.FirstTextSourceIndex + line.Length;
                Assert.True(end > 0 && (char.IsWhiteSpace(words[end - 1]) || end < words.Length && char.IsWhiteSpace(words[end])), "The heading splits a word to make room for Open story.");
            }
            NativeCapture.Save(window, $"today-readable-{width}-{large}.png");
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void DemoHomeShowsTodaySoFarAndOpensTheWholeStory(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            var card = home.FindControl<Border>("TodayCard")!;
            Assert.True(card.IsVisible);
            var today = home.FindControl<TodaySoFar>("TodaySoFar")!;
            Assert.Equal(5, today.Lane.Intervals.Count);
            Assert.True(today.Day!.HasFuture);
            Assert.Equal("Sample day · 5 reports so far · 1 Lucia request", today.FindControl<TextBlock>("TodayCaption")!.Text);
            var main = home.FindControl<StackPanel>("MainPanel")!;
            var side = home.FindControl<StackPanel>("SidePanel")!;
            if (home.IsExpanded)
            {
                Assert.Equal(1, side.Children.IndexOf(card));
                Assert.Equal(2, side.Children.IndexOf(home.FindControl<Border>("DesktopDetailCard")!));
            }
            else
            {
                Assert.Equal(main.Children.IndexOf(home.FindControl<Border>("PlacesCard")!) + 1, main.Children.IndexOf(card));
            }
            NativeCapture.Save(window, $"today-{width}-{(dark ? "dark" : "light")}.png");

            state.SelectPlace("garden");
            ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            (Assert.IsType<StoryScreen>(shell.CurrentScreen)).State.SelectPlace("garden");
            ShellNavigationTests.Navigate(shell, ShellDestination.Home);
            today.FindControl<Button>("OpenTodayStory")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Assert.Equal(SampleStory.Today, story.State.Day.Date);
            Assert.Null(story.State.SelectedPlaceId);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ConnectedHomeHasNoTodayStripUntilHistoryExists()
    {
        var setup = new HomeTopology("home", "Our home", [new("hall", "Hall", PlaceState.Quiet, "Recent source reports.", [])], []);
        var view = new HomeScreen(new HomeViewModel(setup, isDemo: false));
        var window = new Window { Width = 390, Height = 844, Content = view };
        try
        {
            window.Show();
            Assert.False(view.FindControl<Border>("TodayCard")!.IsVisible);
        }
        finally { window.Close(); }
    }
}
