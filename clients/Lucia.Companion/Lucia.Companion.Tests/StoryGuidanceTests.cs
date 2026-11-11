using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class StoryGuidanceTests
{
    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(1194, false)]
    [InlineData(1194, true)]
    public void DayAndWeekExplainTheirInteractionWithoutHover(int width, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var navigator = SampleStory.Navigator(SampleHome.Create());
        var view = new StoryScreen(navigator);
        var window = new Window { Width = width, Height = 834, Content = view };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var help = view.FindControl<TextBlock>("StoryInteractionHint");
            Assert.NotNull(help);
            Assert.True(help.IsEffectivelyVisible);
            Assert.Contains("dial", help.Text);
            Assert.Contains("slider", help.Text);
            Assert.True(help.Bounds.Width <= width - 32);
            NativeCapture.Save(window, $"story-guidance-day-{width}-{large}.png");
            var week = view.FindControl<RadioButton>("WeekMode")!;
            week.BringIntoView(); Dispatcher.UIThread.RunJobs(); Assert.True(week.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null); Dispatcher.UIThread.RunJobs();
            Assert.Equal(StoryMode.Week, navigator.Mode);
            Assert.Contains("Select a day", help.Text);
            Assert.DoesNotContain("dial", help.Text);
            Assert.True(view.FindControl<StoryWeek>("Week")!.IsEffectivelyVisible);
            NativeCapture.Save(window, $"story-guidance-week-{width}-{large}.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }
}
