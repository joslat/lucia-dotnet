using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeSaveProgressTests
{
    [Fact]
    public async Task QueuedWritesStayBusyUntilTheLastCheckedSnapshotIsSaved()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-progress-");
        var file = Path.Combine(directory.FullName, "home.json");
        try
        {
            var state = new HomeViewModel(SampleHome.Create());
            var session = new HomeProfileSession(state, new LocalHomeStore(file));
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            using (var blocker = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                state.SelectPlace("garden"); state.TogglePin(); state.TogglePin(); state.TogglePin();
                state.SetSectionOrder([HomeSection.LuciaNow, HomeSection.Places, HomeSection.Attention]);
                Assert.True(session.IsSaving);
                Assert.True(session.RequiresGlobalNotice);
                Assert.False(session.IsLoading);
                Assert.Equal(0, session.ConfirmationVersion);
                Assert.False(session.PendingSave.IsCompleted);
            }
            await session.PendingSave;
            Assert.False(session.IsSaving);
            Assert.False(session.RequiresGlobalNotice);
            Assert.False(session.HasConflict);
            Assert.Equal(1, session.ConfirmationVersion);
            var saved = await new LocalHomeStore(file).LoadAsync(TestContext.Current.CancellationToken);
            Assert.Contains("garden", saved!.PinnedPlaceIds);
            Assert.Equal(HomeSection.LuciaNow, saved.SectionOrder![0]);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task AFailedWriteKeepsRecoveryVisibleUntilRetryActuallySucceeds()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-failure-");
        var file = Path.Combine(directory.FullName, "home.json");
        try
        {
            var state = new HomeViewModel(SampleHome.Create());
            var session = new HomeProfileSession(state, new LocalHomeStore(file));
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            Directory.CreateDirectory(file);
            state.SelectPlace("garden"); state.TogglePin(); await session.PendingSave;
            Assert.False(session.IsSaving);
            Assert.True(session.CanRetry);
            Assert.True(session.RequiresGlobalNotice);
            Assert.Equal(0, session.ConfirmationVersion);
            Directory.Delete(file);
            session.RetrySave(); await session.PendingSave;
            Assert.False(session.IsSaving);
            Assert.False(session.RequiresGlobalNotice);
            Assert.Equal(1, session.ConfirmationVersion);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task AConflictCannotBeReviewedWhileOlderQueuedWritesAreDraining()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-save-queue-conflict-");
        var file = Path.Combine(directory.FullName, "home.json");
        try
        {
            var state = new HomeViewModel(SampleHome.Create());
            var store = new LocalHomeStore(file);
            var session = new HomeProfileSession(state, store);
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            await store.SaveAsync(HomeProfile.From(state.Home, []), null, TestContext.Current.CancellationToken);
            var guarded = false;
            session.Changed += () =>
            {
                if (!session.HasConflict || !session.IsSaving) { return; }
                guarded = true;
                Assert.True(session.ReviewConflictAsync().IsCompletedSuccessfully);
                Assert.False(session.IsLoading);
                Assert.Null(session.ConflictSnapshot);
            };
            state.SelectPlace("garden"); state.TogglePin(); state.TogglePin(); state.TogglePin();
            await session.PendingSave;
            Assert.True(guarded);
            Assert.False(session.IsSaving);
            Assert.True(session.HasConflict);
            Assert.True(session.RequiresGlobalNotice);
            await session.ReviewConflictAsync();
            Assert.NotNull(session.ConflictSnapshot);
        }
        finally { directory.Delete(recursive: true); }
    }
}
