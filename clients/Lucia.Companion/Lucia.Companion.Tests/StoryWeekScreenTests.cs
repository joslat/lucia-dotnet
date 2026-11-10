using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class StoryWeekScreenTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void StoryOpensOnTodayAndSwitchesDatesAndToTheWeek(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Assert.Equal("Today · Sat 24 Oct", story.FindControl<TextBlock>("DateLabel")!.Text);
            Assert.False(story.FindControl<Button>("NextDate")!.IsEnabled);
            Assert.True(story.FindControl<RadioButton>("DayMode")!.IsChecked);
            NativeCapture.Save(window, $"story-day-{width}-{(dark ? "dark" : "light")}.png");

            Click(story.FindControl<Button>("PreviousDate")!);
            Assert.Equal("Yesterday · Fri 23 Oct", story.FindControl<TextBlock>("DateLabel")!.Text);
            Assert.Equal(new DateOnly(2026, 10, 23), story.State.Day.Date);
            Assert.True(story.FindControl<Button>("NextDate")!.IsEnabled);

            story.FindControl<RadioButton>("WeekMode")!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("18–24 Oct", story.FindControl<TextBlock>("DateLabel")!.Text);
            var week = story.FindControl<StoryWeek>("Week")!;
            Assert.True(week.IsEffectivelyVisible);
            Assert.False(story.FindControl<Grid>("StoryGrid")!.IsVisible);
            Assert.Equal(7, week.Rows.Count);
            Assert.All(week.Rows, row => Assert.True(row.Bounds.Height >= 48));
            NativeCapture.Save(window, $"story-week-{width}-{(dark ? "dark" : "light")}.png");

            Click(week.Rows[2]);
            Assert.True(story.FindControl<RadioButton>("DayMode")!.IsChecked);
            Assert.Equal("Tue 20 Oct", story.FindControl<TextBlock>("DateLabel")!.Text);
            Assert.True(story.FindControl<Grid>("StoryGrid")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ASingleDayStoryHasNoDateNavigation()
    {
        var view = new StoryScreen(new StoryViewModel(SampleStory.Create(SampleHome.Create())));
        var window = new Window { Width = 390, Height = 844, Content = view };
        try
        {
            window.Show();
            Assert.False(view.FindControl<Control>("StoryNavigation")!.IsVisible);
            Assert.False(view.FindControl<StoryWeek>("Week")!.IsVisible);
        }
        finally { window.Close(); }
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
