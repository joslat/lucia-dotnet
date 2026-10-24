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

public sealed class StoryScreenTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void OrbitChaptersAndTimelinesShareSelection(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var state = new StoryViewModel(SampleStory.Create(SampleHome.Create()));
        var view = new StoryScreen(state);
        var window = new Window { Width = width, Height = height, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(width >= 900, view.IsExpanded);
            var orbit = view.FindControl<StoryOrbit>("Orbit")!;
            Assert.Same(state, orbit.State);
            Assert.InRange(orbit.RenderedPlaceCount, 1, 6);
            NativeCapture.Save(window, $"story-{width}-{(dark ? "dark" : "light")}.png");
            var chapter = state.VisibleChapters.Last();
            view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Tag, chapter.Id))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(chapter.Start, state.SelectedInstant);
            Assert.All(view.GetVisualDescendants().OfType<PlaceTimeline>(), timeline => Assert.Same(state, timeline.State));
            view.FindControl<CheckBox>("AnimateSelection")!.IsChecked = false;
            view.FindControl<Slider>("TimeScrubber")!.Value = .5;
            Assert.Null(state.SelectedChapterId);
            Assert.Equal(state.Day.InstantAt(.5), state.SelectedInstant);
            view.FindControl<ScrollViewer>("StoryScroll")!.Offset = new Vector(0, 660);
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"story-lower-{width}-{(dark ? "dark" : "light")}.png");
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(40)]
    public void EmptyAndManyPlacesNeverCompressDozensOfRings(int count)
    {
        var home = new HomeTopology("layout", "Layout", Enumerable.Range(0, count).Select(index =>
            new Place($"place-{index}", $"A longer place name {index}", PlaceState.Unavailable, "No history", [])), []);
        var state = new StoryViewModel(SampleStory.Create(home));
        var view = new StoryScreen(state);
        var window = new Window { Width = 360, Height = 800, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Math.Min(count, 6), view.FindControl<StoryOrbit>("Orbit")!.RenderedPlaceCount);
            Assert.Equal(count, view.GetVisualDescendants().OfType<PlaceTimeline>().Count());
            Assert.Empty(state.VisibleChapters);
            if (count > 0)
            {
                state.SelectPlace($"place-{count - 1}");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, view.FindControl<StoryOrbit>("Orbit")!.RenderedPlaceCount);
                Assert.Single(view.GetVisualDescendants().OfType<PlaceTimeline>());
            }
        }
        finally { window.Close(); }
    }
}
