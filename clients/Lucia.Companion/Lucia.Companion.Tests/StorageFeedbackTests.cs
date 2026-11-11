using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class StorageFeedbackTests
{
    [AvaloniaFact]
    public async Task SaveProgressFollowsNavigationButItsConfirmationDoesNotResurfaceLater()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-navigation-");
        var file = Path.Combine(directory.FullName, "home.json");
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, new LocalHomeStore(file));
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            using (var blocker = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                state.SelectPlace("garden"); state.TogglePin();
                ShellNavigationTests.Navigate(shell, ShellDestination.Story);
                await Task.Delay(220, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
                Assert.True(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
                Assert.True(shell.FindControl<OperationProgress>("LocalProfileOperationProgress")!.IsBusy);
                Assert.True(shell.FindControl<ContentControl>("ScreenHost")!.IsEnabled);
                Assert.True(shell.Navigation.IsEnabled);
            }
            await shell.ProfileSession.PendingSave; Dispatcher.UIThread.RunJobs();
            Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            ShellNavigationTests.Navigate(shell, ShellDestination.Home);
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            Assert.False(home.FindControl<TextBlock>("HomeSaveFeedback")!.IsVisible);
            state.CloseDetail(); state.TogglePin(); await shell.ProfileSession.PendingSave; Dispatcher.UIThread.RunJobs();
            Assert.True(home.FindControl<TextBlock>("HomeSaveFeedback")!.IsEffectivelyVisible);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(home.FindControl<TextBlock>("HomeSaveFeedback")!));
            ShellNavigationTests.Navigate(shell, ShellDestination.More);
            Assert.Contains("saved on this device", Assert.IsType<MoreScreen>(shell.CurrentScreen).FindControl<TextBlock>("HomeStorageContext")!.Text, StringComparison.Ordinal);
            ShellNavigationTests.Navigate(shell, ShellDestination.Home);
            Assert.False(home.FindControl<TextBlock>("HomeSaveFeedback")!.IsVisible);
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; directory.Delete(recursive: true); }
    }

    [AvaloniaFact]
    public async Task RetryingFromStoryKeepsKeyboardFocusWhileTheFooterChangesAndThenCloses()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-retry-focus-");
        var file = Path.Combine(directory.FullName, "home.json");
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, new LocalHomeStore(file));
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            Directory.CreateDirectory(file);
            state.SelectPlace("garden"); state.TogglePin(); await shell.ProfileSession.PendingSave;
            ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var retry = shell.FindControl<Button>("RetrySaveButton")!;
            Assert.True(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            Assert.True(retry.Focus());
            Directory.Delete(file);
            using (var blocker = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(shell.ProfileSession.IsSaving);
                Assert.Same(shell.FindControl<TextBlock>("StorageStatus")!, window.FocusManager!.GetFocusedElement());
                Assert.False(retry.IsEnabled);
            }
            await shell.ProfileSession.PendingSave; Dispatcher.UIThread.RunJobs();
            Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            Assert.Same(shell.Navigation.ButtonFor(ShellDestination.Story), window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; directory.Delete(recursive: true); }
    }

    [AvaloniaFact]
    public async Task SetupAndPagePinConfirmOnlyCompletedPersistentWritesAtTheVisiblePlace()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-local-");
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, new LocalHomeStore(Path.Combine(directory.FullName, "home.json")));
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            ShellNavigationTests.OpenSetup(shell);
            var editor = Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
            editor.FindControl<TextBox>("HomeNameInput")!.Text = "My saved home";
            Click(editor.FindControl<Button>("SaveHome")!);
            await shell.ProfileSession.PendingSave; Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            Assert.True(home.FindControl<TextBlock>("HomeSaveFeedback")!.IsEffectivelyVisible);
            Assert.Equal("My saved home", home.FindControl<TextBlock>("HomeName")!.Text);
            state.SelectPlace("garden"); Dispatcher.UIThread.RunJobs();
            var page = home.FindControl<PlacePage>("PlacePage")!;
            Click(page.FindControl<ToggleButton>("PinToggle")!);
            await shell.ProfileSession.PendingSave; Dispatcher.UIThread.RunJobs();
            Assert.True(page.FindControl<TextBlock>("PageSaveFeedback")!.IsEffectivelyVisible);
            NativeCapture.Save(window, "local-page-save-confirmation.png");
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; directory.Delete(recursive: true); }
    }

    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
}
