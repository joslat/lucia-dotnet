using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class SaveNoticeTimingTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANewlyQueuedQuickWriteDoesNotFlashAGlobalFooter(bool reduced)
    {
        MotionSettings.Current.ReduceMotion = reduced;
        var directory = Directory.CreateTempSubdirectory("lucia-save-notice-");
        var file = Path.Combine(directory.FullName, "home.json");
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, new LocalHomeStore(file));
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            var height = shell.FindControl<ContentControl>("ScreenHost")!.Bounds.Height;
            using (var blocker = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                state.SelectPlace("garden"); state.CloseDetail(); state.TogglePin();
                Assert.True(shell.ProfileSession.IsSaving);
                Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            }
            await shell.ProfileSession.PendingSave; Dispatcher.UIThread.RunJobs();
            Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            Assert.Equal(height, shell.FindControl<ContentControl>("ScreenHost")!.Bounds.Height);
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; MotionSettings.Current.ReduceMotion = true; directory.Delete(recursive: true); }
    }

    [AvaloniaFact]
    public async Task AContinuingWriteShowsRealProgressAndKeepsNavigationAvailable()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-notice-pending-");
        var file = Path.Combine(directory.FullName, "home.json");
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, new LocalHomeStore(file));
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            using (var blocker = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                state.SelectPlace("garden"); state.CloseDetail(); state.TogglePin();
                await Task.Delay(100, TestContext.Current.CancellationToken); state.TogglePin();
                await Task.Delay(120, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
                Assert.True(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
                Assert.True(shell.FindControl<OperationProgress>("LocalProfileOperationProgress")!.IsBusy);
                Assert.True(shell.Navigation.IsEnabled);
                NativeCapture.Save(window, "continuing-save-notice.png");
            }
            await shell.ProfileSession.PendingSave; Dispatcher.UIThread.RunJobs();
            Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; directory.Delete(recursive: true); }
    }

    [AvaloniaFact]
    public async Task PausingStopsADelayedRevealAndResumeUsesTheActualElapsedSavingTime()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-notice-pause-");
        var file = Path.Combine(directory.FullName, "home.json");
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, new LocalHomeStore(file));
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            MotionSettings.Current.Resume();
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            using (var blocker = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                state.SelectPlace("garden"); state.CloseDetail(); state.TogglePin(); MotionSettings.Current.Pause();
                await Task.Delay(220, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
                Assert.True(shell.ProfileSession.IsSaving);
                Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
                MotionSettings.Current.Resume(); Dispatcher.UIThread.RunJobs();
                Assert.True(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            }
            await shell.ProfileSession.PendingSave;
            Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; MotionSettings.Current.Resume(); directory.Delete(recursive: true); }
    }

    [AvaloniaFact]
    public async Task AWriteCompletedWhileHiddenDoesNotLeaveProgressBehindOnReturn()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-notice-hidden-");
        var file = Path.Combine(directory.FullName, "home.json");
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, new LocalHomeStore(file));
        var parent = new Border { Child = shell };
        var window = new Window { Width = 390, Height = 844, Content = parent };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            using (var blocker = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                state.SelectPlace("garden"); state.CloseDetail(); state.TogglePin(); parent.IsVisible = false;
                await Task.Delay(220, TestContext.Current.CancellationToken);
            }
            await shell.ProfileSession.PendingSave; parent.IsVisible = true; Dispatcher.UIThread.RunJobs();
            Assert.False(shell.FindControl<StackPanel>("StorageFooter")!.IsVisible);
            Assert.False(shell.FindControl<OperationProgress>("LocalProfileOperationProgress")!.IsBusy);
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; directory.Delete(recursive: true); }
    }
}
