using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ResponsiveWeekTests
{
    [AvaloniaTheory]
    [InlineData(360)]
    [InlineData(390)]
    [InlineData(600)]
    [InlineData(1194)]
    public void LiveLargeTextKeepsWeekWordsAndRulerReadableWithoutReplacingFocusedActions(int width)
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            story.FindControl<RadioButton>("WeekMode")!.IsChecked = true; Dispatcher.UIThread.RunJobs();
            var week = story.FindControl<StoryWeek>("Week")!;
            var original = week.Rows[2]; original.Focus(); Dispatcher.UIThread.RunJobs();
            CompanionTypography.Apply(size => size * 2); Dispatcher.UIThread.RunJobs();
            Assert.Same(original, week.Rows[2]);
            Assert.Same(original, window.FocusManager!.GetFocusedElement());
            foreach (var label in week.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Text?.Contains("report", StringComparison.Ordinal) == true && text.Parent is Grid))
            {
                Assert.Single(label.TextLayout.TextLines);
                Assert.True(label.TextLayout.TextLines[0].Width <= label.Bounds.Width + .1);
            }
            var ticks = week.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && text.Text is "00" or "06" or "12" or "18").OrderBy(text => text.TranslatePoint(default, week)!.Value.X).ToArray();
            for (var i = 1; i < ticks.Length; i++)
            {
                var previousEnd = ticks[i - 1].TranslatePoint(default, week)!.Value.X + ticks[i - 1].TextLayout.TextLines[0].Width;
                Assert.True(ticks[i].TranslatePoint(default, week)!.Value.X >= previousEnd + 4);
            }
            foreach (var button in shell.Navigation.Destinations.Select(shell.Navigation.ButtonFor))
            {
                var label = button.GetVisualDescendants().OfType<TextBlock>().Single();
                Assert.Single(label.TextLayout.TextLines);
                Assert.True(label.TextLayout.TextLines[0].Width <= label.Bounds.Width + .1);
            }
            week.Rows[0].BringIntoView(); Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"week-readable-{width}.png");
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }
}
