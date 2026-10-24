using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeSaveConflictTests
{
    [Fact]
    public async Task SeparateWritersCannotReplaceTheSameRevision()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-");
        try
        {
            var path = Path.Combine(directory.FullName, "home.json");
            var first = new LocalHomeStore(path);
            var second = new LocalHomeStore(path);
            var original = HomeProfile.From(SampleHome.Create(), []);
            var revision = await first.SaveAsync(original, null, TestContext.Current.CancellationToken);
            var attempts = new[] { first, second }.Select(async (store, index) =>
            {
                try
                {
                    await store.SaveAsync(original with { HomeName = $"Writer {index}" }, revision, TestContext.Current.CancellationToken);
                    return true;
                }
                catch (HomeSaveConflictException) { return false; }
            });
            var results = await Task.WhenAll(attempts);
            Assert.Single(results, result => result);
            Assert.Single(results, result => !result);
            Assert.Empty(Directory.GetFiles(directory.FullName, "*.tmp"));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task CorruptSnapshotReplacementAlsoRequiresTheReviewedRevision()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-");
        try
        {
            var path = Path.Combine(directory.FullName, "home.json");
            await File.WriteAllTextAsync(path, "broken", TestContext.Current.CancellationToken);
            var store = new LocalHomeStore(path);
            var broken = await store.LoadSnapshotAsync(TestContext.Current.CancellationToken);
            Assert.True(broken.IsUnreadable);
            Assert.NotNull(broken.Revision);
            var profile = HomeProfile.From(SampleHome.Create(), []);
            await new LocalHomeStore(path).SaveAsync(profile, broken.Revision, TestContext.Current.CancellationToken);
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<HomeSaveConflictException>(() => store.SaveAsync(profile with { HomeName = "Stale repair" }, broken.Revision, TestContext.Current.CancellationToken));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task ConflictPreservesBothVersionsAndRequiresAReviewedChoice()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-");
        try
        {
            var path = Path.Combine(directory.FullName, "home.json");
            var firstState = new HomeViewModel(SampleHome.Create());
            var secondState = new HomeViewModel(SampleHome.Create());
            var first = new HomeProfileSession(firstState, new LocalHomeStore(path));
            var second = new HomeProfileSession(secondState, new LocalHomeStore(path));
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            await second.InitializeAsync(TestContext.Current.CancellationToken);
            first.ApplySetup(Rename(firstState.Home, "Saved elsewhere"));
            await first.PendingSave;
            second.ApplySetup(Rename(secondState.Home, "My unsaved home"));
            await second.PendingSave;
            Assert.True(second.HasConflict);
            Assert.False(second.CanRetry);
            Assert.Equal("My unsaved home", secondState.Home.Name);
            second.RetrySave();
            await second.PendingSave;
            Assert.Equal("Saved elsewhere", (await new LocalHomeStore(path).LoadAsync(TestContext.Current.CancellationToken))!.HomeName);
            Assert.False(await second.ResolveConflictAsync(keepSessionChanges: true));
            await second.ReviewConflictAsync();
            Assert.Equal("Saved elsewhere", second.ConflictSnapshot!.Profile!.HomeName);
            Assert.True(await second.ResolveConflictAsync(keepSessionChanges: true));
            Assert.False(second.HasConflict);
            Assert.Equal("My unsaved home", (await new LocalHomeStore(path).LoadAsync(TestContext.Current.CancellationToken))!.HomeName);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task ChangedReviewCannotOverwriteOrDiscardSessionAndReloadRestoresPreferences()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-");
        try
        {
            var path = Path.Combine(directory.FullName, "home.json");
            var store = new LocalHomeStore(path);
            var state = new HomeViewModel(SampleHome.Create());
            var session = new HomeProfileSession(state, store);
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            var saved = HomeProfile.From(Rename(state.Home, "First saved"), ["garden"], [HomeSection.LuciaNow, HomeSection.Places, HomeSection.Attention]);
            var revision = await store.SaveAsync(saved, null, TestContext.Current.CancellationToken);
            session.ApplySetup(Rename(state.Home, "My changes"));
            await session.PendingSave;
            await session.ReviewConflictAsync();
            await store.SaveAsync(saved with { HomeName = "Newest saved" }, revision, TestContext.Current.CancellationToken);
            Assert.False(await session.ResolveConflictAsync(keepSessionChanges: false));
            Assert.Equal("My changes", state.Home.Name);
            Assert.True(session.HasConflict);
            Assert.Equal("Newest saved", session.ConflictSnapshot!.Profile!.HomeName);
            Assert.True(await session.ResolveConflictAsync(keepSessionChanges: false));
            Assert.Equal("Newest saved", state.Home.Name);
            Assert.True(state.IsPinned("garden"));
            Assert.Equal(HomeSection.LuciaNow, state.SectionOrder[0]);
            state.SetSectionOrder(HomeSections.DefaultOrder);
            await session.PendingSave;
            Assert.False(session.HasConflict);
        }
        finally { directory.Delete(recursive: true); }
    }

    private static HomeTopology Rename(HomeTopology home, string name) => new(home.Id, name, home.Places, home.Connections, home.Bindings);

    [Fact]
    public async Task LockedWriterCancelsWithoutTouchingSavedBytesOrLeavingTemporaryFiles()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-");
        try
        {
            var path = Path.Combine(directory.FullName, "home.json");
            var store = new LocalHomeStore(path);
            var profile = HomeProfile.From(SampleHome.Create(), []);
            var revision = await store.SaveAsync(profile, null, TestContext.Current.CancellationToken);
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            await using var heldLock = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LocalHomeStore(path).SaveAsync(profile with { HomeName = "Blocked" }, revision, cancellation.Token));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFiles(directory.FullName, "*.tmp"));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task ExistingFileCannotBeCreatedAgainAndDeletedRevisionCannotBeReplaced()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-");
        try
        {
            var path = Path.Combine(directory.FullName, "home.json");
            var store = new LocalHomeStore(path);
            var profile = HomeProfile.From(SampleHome.Create(), []);
            var revision = await store.SaveAsync(profile, null, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<HomeSaveConflictException>(() => store.SaveAsync(profile, null, TestContext.Current.CancellationToken));
            File.Delete(path);
            await Assert.ThrowsAsync<HomeSaveConflictException>(() => store.SaveAsync(profile, revision, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReviewHandlesUnreadableOrRemovedFilesWithoutDiscardingChanges(bool corrupt)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-");
        try
        {
            var path = Path.Combine(directory.FullName, "home.json");
            var store = new LocalHomeStore(path);
            var state = new HomeViewModel(SampleHome.Create());
            var initialRevision = await store.SaveAsync(HomeProfile.From(state.Home, []), null, TestContext.Current.CancellationToken);
            var session = new HomeProfileSession(state, store);
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            if (corrupt) { await File.WriteAllTextAsync(path, "broken later", TestContext.Current.CancellationToken); }
            else { File.Delete(path); }
            session.ApplySetup(Rename(state.Home, "Preserved session"));
            await session.PendingSave;
            Assert.True(session.HasConflict);
            await session.ReviewConflictAsync();
            Assert.False(session.CanUseSavedSetup);
            Assert.False(await session.ResolveConflictAsync(keepSessionChanges: false));
            Assert.Equal("Preserved session", state.Home.Name);
            Assert.True(await session.ResolveConflictAsync(keepSessionChanges: true));
            Assert.Equal("Preserved session", (await store.LoadAsync(TestContext.Current.CancellationToken))!.HomeName);
            Assert.NotEqual(initialRevision, (await store.LoadSnapshotAsync(TestContext.Current.CancellationToken)).Revision);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task OversizedUnreadProfileCannotBeBlindlyReplaced()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-conflict-");
        try
        {
            var path = Path.Combine(directory.FullName, "home.json");
            await File.WriteAllBytesAsync(path, new byte[1_048_577], TestContext.Current.CancellationToken);
            var state = new HomeViewModel(SampleHome.Create());
            var session = new HomeProfileSession(state, new LocalHomeStore(path));
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            Assert.True(session.HasUnreadableProfile);
            Assert.False(session.CanReplaceUnreadableProfile);
            Assert.Throws<InvalidOperationException>(() => session.ApplySetup(state.Home, replaceUnreadableProfile: true));
            Assert.Equal(1_048_577, new FileInfo(path).Length);
        }
        finally { directory.Delete(recursive: true); }
    }
}
