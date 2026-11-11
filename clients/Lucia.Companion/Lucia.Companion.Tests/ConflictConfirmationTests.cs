using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConflictConfirmationTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ACompletedConflictChoiceConfirmsItsActualOutcomeOnHome(bool keepSession)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-confirm-");
        var state = new HomeViewModel(SampleHome.Create());
        var store = new LocalHomeStore(Path.Combine(directory.FullName, "home.json"));
        var shell = new CompanionShell(state, store);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            await store.SaveAsync(HomeProfile.From(state.Home, []) with { HomeName = "Saved elsewhere" }, null, TestContext.Current.CancellationToken);
            state.SelectPlace("garden"); state.TogglePin(); state.CloseDetail();
            await shell.ProfileSession.PendingSave;
            shell.FindControl<Button>("ReviewSaveConflict")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 100 && shell.ProfileSession.IsLoading; i++) { await Task.Delay(10, TestContext.Current.CancellationToken); }
            Assert.False(shell.ProfileSession.IsLoading);
            var review = Assert.IsType<HomeSaveConflictScreen>(shell.CurrentScreen);
            review.FindControl<Button>(keepSession ? "KeepSessionChanges" : "UseSavedSetup")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 100 && shell.CurrentScreen is not HomeScreen; i++) { await Task.Delay(10, TestContext.Current.CancellationToken); }
            Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            var feedback = home.FindControl<TextBlock>("HomeSaveFeedback")!;
            Assert.True(feedback.IsEffectivelyVisible);
            Assert.Equal(keepSession ? "Home setup saved on this device." : "Using the saved setup.", feedback.Text);
            Assert.Equal(keepSession ? "Sample home" : "Saved elsewhere", state.Home.Name);
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; directory.Delete(recursive: true); }
    }
}
