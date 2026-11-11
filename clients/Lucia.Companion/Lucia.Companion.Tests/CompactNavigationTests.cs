using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompactNavigationTests
{
    [AvaloniaFact]
    public void LiveTextChangesChooseAFittingLayoutWithoutReplacingOrDefocusingNavigation()
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 1194, Height = 420, Content = shell };
        try
        {
            window.Show(); ShellNavigationTests.Navigate(shell, ShellDestination.More);
            var button = shell.Navigation.ButtonFor(ShellDestination.More); button.Focus();
            Assert.True(shell.Navigation.IsRail);
            CompanionTypography.Apply(size => size * 2); Dispatcher.UIThread.RunJobs();
            Assert.False(shell.Navigation.IsRail);
            Assert.Same(button, shell.Navigation.ButtonFor(ShellDestination.More));
            Assert.Same(button, window.FocusManager!.GetFocusedElement());
            CompanionTypography.Apply(size => size); Dispatcher.UIThread.RunJobs();
            Assert.True(shell.Navigation.IsRail);
            Assert.Same(button, window.FocusManager.GetFocusedElement());
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(844, 390, false, false, false)]
    [InlineData(844, 390, true, true, false)]
    [InlineData(600, 360, true, false, false)]
    [InlineData(1194, 360, true, true, false)]
    [InlineData(390, 500, true, true, true)]
    [InlineData(844, 390, true, false, true)]
    [InlineData(1194, 500, true, true, false)]
    [InlineData(390, 500, false, false, false)]
    public async Task EveryDestinationStaysVisibleAndWorksInCompactWindows(int width, int height, bool large, bool dark, bool failedSave)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-compact-nav-");
        var file = Path.Combine(directory.FullName, "home.json");
        var theme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, failedSave ? new LocalHomeStore(file) : null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            if (failedSave)
            {
                Directory.CreateDirectory(file); state.SelectPlace("garden"); state.CloseDetail(); state.TogglePin();
                await shell.ProfileSession.PendingSave;
                Assert.True(shell.ProfileSession.CanRetry);
                Assert.True(shell.FindControl<ContentControl>("ScreenHost")!.IsEnabled);
            }
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"compact-home-{width}-{height}-{large}-{dark}-{failedSave}.png");
            foreach (var destination in shell.Navigation.Destinations)
            {
                var button = shell.Navigation.ButtonFor(destination);
                var position = button.TranslatePoint(default, window)!.Value;
                Assert.True(position.Y >= 0 && position.Y + button.Bounds.Height <= height + .1,
                    $"{destination} extends outside the {width}x{height} viewport: top {position.Y}, height {button.Bounds.Height}.");
                Assert.True(position.X >= 0 && position.X + button.Bounds.Width <= width + .1);
                Assert.True(button.Focus());
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(destination, shell.Navigation.Selected);
                Assert.Same(button, window.FocusManager!.GetFocusedElement());
                Assert.Single(button.GetVisualDescendants().OfType<TextBlock>().Single().TextLayout.TextLines);
            }
            NativeCapture.Save(window, $"compact-more-{width}-{height}-{large}-{dark}-{failedSave}.png");
        }
        finally
        {
            window.Close(); await shell.ProfileSession.PendingSave;
            CompanionTypography.Apply(size => size); Application.Current.RequestedThemeVariant = theme;
            directory.Delete(recursive: true);
        }
    }
}
