using Avalonia;
using Avalonia.Automation;
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

public sealed class HomeHeaderTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void DemoHeaderLeadsWithWhatNeedsYou(int width, int height, bool dark)
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
            Assert.Equal("Saturday 24 Oct · 12:00 UTC", home.FindControl<TextBlock>("HomeDate")!.Text);
            Assert.Equal("3 things need you", home.FindControl<TextBlock>("AttentionHeadline")!.Text);
            Assert.Equal("Utility. A sample water sensor reports a leak.", home.FindControl<TextBlock>("AttentionSummary")!.Text);
            Assert.Equal("Offline demo", home.FindControl<TextBlock>("HomeModeLabel")!.Text);
            Assert.False(home.FindControl<Control>("LiveDot")!.IsVisible);
            Assert.Contains("Synthetic", home.FindControl<TextBlock>("SourceNotice")!.Text, StringComparison.Ordinal);
            var name = home.FindControl<TextBlock>("HomeName")!;
            var headline = home.FindControl<TextBlock>("AttentionHeadline")!;
            var namePoint = name.TranslatePoint(default, home)!.Value;
            var headlinePoint = headline.TranslatePoint(default, home)!.Value;
            if (home.IsExpanded)
            {
                // Tablet: the headline sits beside the home's name, after a divider.
                Assert.True(headlinePoint.X > namePoint.X + name.Bounds.Width);
                Assert.True(home.FindControl<Control>("HeaderDivider")!.IsVisible);
            }
            else
            {
                Assert.True(headlinePoint.Y > namePoint.Y + name.Bounds.Height);
                Assert.False(home.FindControl<Control>("HeaderDivider")!.IsVisible);
            }
            NativeCapture.Save(window, $"header-{width}-{(dark ? "dark" : "light")}.png");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NeedsYouRowsShowKindReasonAgeAndOpenTheirPlace()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            var list = home.FindControl<AttentionList>("Attention")!;
            Assert.Equal("Needs you", list.Heading);
            Assert.Equal(["utility", "porch", "bedroom"], list.Rows.Select(row => row.Tag));
            Assert.Contains("lead", list.Rows[0].Classes);
            Assert.All(list.Rows.Skip(1), row => Assert.DoesNotContain("lead", row.Classes));
            Assert.Equal(["problem", "waiting", "unavailable"], list.Rows.Select(row => row.Classes.Single(name => name is "problem" or "waiting" or "unavailable")));
            Assert.Equal(["Utility · Reported problem", "A sample water sensor reports a leak.", "Latest reading just now"], Texts(list.Rows[0]));
            Assert.Equal(["Bedroom · Data unavailable", "The sample source is unavailable; the old value is not current.", "Latest reading 2 h ago"], Texts(list.Rows[2]));
            Assert.Equal("Utility. Reported problem. A sample water sensor reports a leak. Latest reading just now. Show detail.",
                list.Rows[0].GetValue(AutomationProperties.NameProperty));
            Assert.All(list.Rows, row => Assert.True(row.Bounds.Height >= 48));
            list.Rows[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("bedroom", state.SelectedPlace?.Id);
            Assert.True(state.IsDetailOpen);
            state.CloseDetail();
            Dispatcher.UIThread.RunJobs();
            home.FindControl<ScrollViewer>("ContextScroll")!.Offset = new Vector(0, list.TranslatePoint(default, home.FindControl<Grid>("HomeRoot")!)!.Value.Y - 40);
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, "needs-you-390.png");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ConnectedHeaderShowsLiveOnlyWithAReportTime()
    {
        var setup = new HomeTopology("home", "Our home", [new("hall", "Hall", PlaceState.Quiet, "Nothing reported.", [])], []);
        var state = new HomeViewModel(setup, isDemo: false);
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            Assert.Equal("Connected", home.FindControl<TextBlock>("HomeModeLabel")!.Text);
            Assert.False(home.FindControl<Control>("LiveDot")!.IsVisible);
            Assert.False(home.FindControl<TextBlock>("HomeDate")!.IsVisible);
            state.SetReportTime(new DateTimeOffset(2026, 11, 10, 14, 31, 5, TimeSpan.Zero));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Live", home.FindControl<TextBlock>("HomeModeLabel")!.Text);
            Assert.True(home.FindControl<Control>("LiveDot")!.IsVisible);
            Assert.Equal("Tuesday 10 Nov · 14:31 UTC", home.FindControl<TextBlock>("HomeDate")!.Text);
            Assert.Equal("Nothing needs you", home.FindControl<TextBlock>("AttentionHeadline")!.Text);
            Assert.Equal("Your configured reports flag nothing. That isn't a safety or presence check.", home.FindControl<TextBlock>("AttentionSummary")!.Text);
            Assert.Equal("No configured reports flagged", home.FindControl<AttentionList>("Attention")!.Heading);
            Assert.Empty(home.FindControl<AttentionList>("Attention")!.Rows);
            state.SetReportTime(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Connected", home.FindControl<TextBlock>("HomeModeLabel")!.Text);
            Assert.False(home.FindControl<TextBlock>("HomeDate")!.IsVisible);
        }
        finally { window.Close(); }
    }

    private static string[] Texts(Button row) => row.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToArray();
}
