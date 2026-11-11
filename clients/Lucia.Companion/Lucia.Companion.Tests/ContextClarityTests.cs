using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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

public sealed class ContextClarityTests
{
    [AvaloniaTheory]
    [InlineData(390, false, false)]
    [InlineData(390, true, false)]
    [InlineData(1194, false, false)]
    [InlineData(1194, true, false)]
    [InlineData(390, false, true)]
    [InlineData(390, true, true)]
    [InlineData(1194, false, true)]
    [InlineData(1194, true, true)]
    public void IdleContextStaysLocalAndWeekNamesItsModeAndSelectedDay(int width, bool dark, bool large)
    {
        var theme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            NativeCapture.Save(window, $"clarity-home-{width}-{dark}-{large}.png");
            var more = ShellNavigationTests.OpenMore(shell);
            Assert.Contains("session", more.FindControl<TextBlock>("HomeStorageContext")!.Text, StringComparison.OrdinalIgnoreCase);
            NativeCapture.Save(window, $"clarity-more-{width}-{dark}-{large}.png");
            Click(more.FindControl<Button>("OpenHomeSetup")!);
            var editor = Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
            Assert.Contains("Save setup", editor.FindControl<TextBlock>("DraftSaveNotice")!.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Changes save", editor.FindControl<TextBlock>("DraftSaveNotice")!.Text, StringComparison.Ordinal);
            Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            NativeCapture.Save(window, $"clarity-setup-{width}-{dark}-{large}.png");
            Click(editor.FindControl<Button>("CancelEdit")!);
            ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Assert.Equal("The story of your day", story.FindControl<TextBlock>("StoryTitle")!.Text);
            story.FindControl<RadioButton>("WeekMode")!.IsChecked = true; Dispatcher.UIThread.RunJobs();
            Assert.Equal("The story of your week", story.FindControl<TextBlock>("StoryTitle")!.Text);
            var week = story.FindControl<StoryWeek>("Week")!;
            Assert.Single(week.Rows, row => AutomationProperties.GetName(row)!.Contains("Selected day", StringComparison.Ordinal));
            Assert.All(week.Rows, row => Assert.Contains("Open this day", AutomationProperties.GetName(row), StringComparison.Ordinal));
            NativeCapture.Save(window, $"clarity-week-{width}-{dark}-{large}.png");
            Click(week.Rows[2]);
            Assert.Equal("The story of your day", story.FindControl<TextBlock>("StoryTitle")!.Text);
            story.FindControl<RadioButton>("WeekMode")!.IsChecked = true; Dispatcher.UIThread.RunJobs();
            Assert.Contains("Selected day", AutomationProperties.GetName(story.FindControl<StoryWeek>("Week")!.Rows[2]), StringComparison.Ordinal);
        }
        finally { CompanionTypography.Apply(size => size); Application.Current.RequestedThemeVariant = theme; window.Close(); }
    }

    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
}
