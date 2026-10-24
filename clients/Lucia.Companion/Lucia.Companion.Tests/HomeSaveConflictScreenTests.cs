using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeSaveConflictScreenTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false, 1.0)]
    [InlineData(390, 844, true, 1.0)]
    [InlineData(1194, 834, false, 1.0)]
    [InlineData(1194, 834, true, 1.0)]
    [InlineData(390, 844, false, 2.0)]
    [InlineData(390, 844, true, 2.0)]
    [InlineData(1194, 834, false, 2.0)]
    [InlineData(1194, 834, true, 2.0)]
    public async Task ReviewShowsBothVersionsAndBackDoesNotDiscard(int width, int height, bool dark, double scale)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-ui-");
        var theme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var state = new HomeViewModel(SampleHome.Create());
        var store = new LocalHomeStore(Path.Combine(directory.FullName, "home.json"));
        var shell = new CompanionShell(state, store);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            Lucia.Companion.App.CompanionTypography.Apply(size => size * scale);
            await store.SaveAsync(HomeProfile.From(state.Home, []) with { HomeName = "Changed on another app" }, null, TestContext.Current.CancellationToken);
            state.SelectPlace("garden");
            state.TogglePin();
            await shell.ProfileSession.PendingSave;
            Assert.True(shell.FindControl<Button>("ReviewSaveConflict")!.IsVisible);
            Click(shell.FindControl<Button>("ReviewSaveConflict")!);
            await UntilReady(shell.ProfileSession);
            var screen = Assert.IsType<HomeSaveConflictScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Sample home", screen.FindControl<TextBlock>("SessionSummary")!.Text);
            Assert.Contains("Changed on another app", screen.FindControl<TextBlock>("SavedSummary")!.Text);
            Assert.Contains("Garden", screen.FindControl<TextBlock>("SessionSummary")!.Text);
            Assert.True(screen.FindControl<Button>("KeepSessionChanges")!.IsEnabled);
            Assert.True(screen.FindControl<Button>("UseSavedSetup")!.IsEnabled);
            NativeCapture.Save(window, $"conflict-{width}-{(dark ? "dark" : "light")}-{scale:0.0}.png");
            var keep = screen.FindControl<Button>("KeepSessionChanges")!;
            keep.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            var position = keep.TranslatePoint(new Point(0, keep.Bounds.Height), window);
            Assert.NotNull(position);
            Assert.InRange(position.Value.Y, 0, height);
            NativeCapture.Save(window, $"conflict-choice-{width}-{(dark ? "dark" : "light")}-{scale:0.0}.png");
            Assert.True(Back(window));
            Assert.IsType<HomeScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Assert.True(state.IsPinned("garden"));
            Assert.True(shell.ProfileSession.HasConflict);
            Click(shell.FindControl<Button>("ReviewSaveConflict")!);
            await UntilReady(shell.ProfileSession);
            screen = Assert.IsType<HomeSaveConflictScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Click(screen.FindControl<Button>("UseSavedSetup")!);
            await UntilReady(shell.ProfileSession);
            Assert.IsType<HomeScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Assert.False(state.IsPinned("garden"));
            Assert.Equal("Changed on another app", state.Home.Name);
            Assert.False(shell.ProfileSession.HasConflict);
        }
        finally { Lucia.Companion.App.CompanionTypography.Apply(size => size); window.Close(); Application.Current.RequestedThemeVariant = theme; directory.Delete(recursive: true); }
    }

    private static async Task UntilReady(HomeProfileSession session)
    {
        for (var attempt = 0; attempt < 100 && session.IsLoading; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.False(session.IsLoading);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task DifferentHomeExplainsDisabledLoadAndRequiresExplicitCheckedReplacement()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-ui-");
        var theme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        var state = new HomeViewModel(SampleHome.Create());
        var store = new LocalHomeStore(Path.Combine(directory.FullName, "home.json"));
        var shell = new CompanionShell(state, store);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            Lucia.Companion.App.CompanionTypography.Apply(size => size * 2);
            await store.SaveAsync(HomeProfile.From(state.Home, []) with { HomeId = "another-home", HomeName = "Another household setup" }, null, TestContext.Current.CancellationToken);
            state.SelectPlace("garden");
            state.TogglePin();
            await shell.ProfileSession.PendingSave;
            Click(shell.FindControl<Button>("ReviewSaveConflict")!);
            await UntilReady(shell.ProfileSession);
            var screen = Assert.IsType<HomeSaveConflictScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            Assert.Contains("belongs to another home", screen.FindControl<TextBlock>("SavedSummary")!.Text);
            Assert.False(screen.FindControl<Button>("UseSavedSetup")!.IsEnabled);
            Assert.True(screen.FindControl<Button>("KeepSessionChanges")!.IsEnabled);
            screen.FindControl<TextBlock>("SavedSummary")!.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, "conflict-different-home-390-dark-2.0.png");
            Click(screen.FindControl<Button>("KeepSessionChanges")!);
            await UntilReady(shell.ProfileSession);
            Assert.False(shell.ProfileSession.HasConflict);
            Assert.Equal("synthetic-home", (await store.LoadAsync(TestContext.Current.CancellationToken))!.HomeId);
            Assert.True(state.IsPinned("garden"));
        }
        finally { Lucia.Companion.App.CompanionTypography.Apply(size => size); window.Close(); Application.Current.RequestedThemeVariant = theme; directory.Delete(recursive: true); }
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static bool Back(Window window)
    {
        var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(args);
        return args.Handled;
    }
}
