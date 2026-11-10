using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class TypographyTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, 1.3, true)]
    [InlineData(390, 844, 2.0, true)]
    [InlineData(390, 844, 2.0, false)]
    [InlineData(1194, 834, 2.0, true)]
    [InlineData(1194, 834, 2.0, false)]
    public void ExplicitAndStyledTextRespondToPlatformTextSizes(int width, int height, double scale, bool dark)
    {
        var previousTheme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        var state = new Lucia.Companion.App.ViewModels.HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            CompanionTypography.Apply(size => size * scale);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(16 * scale, shell.Navigation.ButtonFor(ShellDestination.Story).FontSize);
            Assert.Equal(13 * scale, shell.FindControl<TextBlock>("StorageStatus")!.FontSize);
            NativeCapture.Save(window, $"text-{width}-{scale:0.0}-{(dark ? "dark" : "light")}-home.png");
            state.SelectPlace("garden");
            state.OpenActions();
            Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Assert.True(home.FindControl<BloomActions>("PlaceActions")!.FindControl<Border>("LargeTextActions")!.IsVisible);
            NativeCapture.Save(window, $"text-{width}-{scale:0.0}-{(dark ? "dark" : "light")}-actions.png");
            state.CloseActions();
            ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(12 * scale, story.FindControl<TextBlock>("TopTime")!.FontSize);
            Assert.True(story.FindControl<TextBlock>("LargeSelectedTime")!.IsVisible);
            NativeCapture.Save(window, $"text-{width}-{scale:0.0}-{(dark ? "dark" : "light")}-story.png");
            var more = ShellNavigationTests.OpenMore(shell);
            Assert.Equal(16 * scale, more.FindControl<RadioButton>("ThemeDark")!.FontSize);
            Assert.Equal(13 * scale, shell.Navigation.ButtonFor(ShellDestination.More).GetVisualDescendants().OfType<TextBlock>().Single().FontSize);
            NativeCapture.Save(window, $"text-{width}-{scale:0.0}-{(dark ? "dark" : "light")}-more.png");
            ShellNavigationTests.OpenSetup(shell);
            var editor = Assert.IsType<HomeEditorScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            editor.FindControl<TextBox>("PlaceNameInput")!.Text = "";
            window.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(TopLevel.BackRequestedEvent));
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"text-{width}-{scale:0.0}-{(dark ? "dark" : "light")}-discard.png");
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); Application.Current.RequestedThemeVariant = previousTheme; }
    }

    [AvaloniaFact]
    public void MappingUsesEachRoleAndRejectsInvalidResultsBeforeChangingResources()
    {
        try
        {
            CompanionTypography.Apply(size => size > 24 ? size * 1.3 : size * 2);
            Assert.Equal(32d, Application.Current!.Resources["TextSize16"]);
            Assert.Equal(41.6, (double)Application.Current.Resources["TextSize32"]!, 6);
            Assert.Throws<ArgumentException>(() => CompanionTypography.Apply(_ => double.NaN));
            Assert.Equal(32d, Application.Current.Resources["TextSize16"]);
        }
        finally { CompanionTypography.Apply(size => size); }
    }
}
