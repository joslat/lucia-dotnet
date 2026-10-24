using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeProfileSessionTests
{
    [Fact]
    public async Task CorruptSetupDisablesAutosaveUntilExplicitReplacement()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lucia-companion-session-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(file, "broken setup", TestContext.Current.CancellationToken);
            var state = new HomeViewModel(SampleHome.Create());
            var session = new HomeProfileSession(state, new LocalHomeStore(file));
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            Assert.True(session.HasUnreadableProfile);
            state.SelectPlace("garden");
            state.TogglePin();
            await session.PendingSave;
            Assert.Equal("broken setup", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
            Assert.Throws<InvalidOperationException>(() => session.ApplySetup(state.Home));
            session.ApplySetup(state.Home, replaceUnreadableProfile: true);
            await session.PendingSave;
            Assert.False(session.HasUnreadableProfile);
            Assert.NotEqual("broken setup", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        }
        finally { if (File.Exists(file)) { File.Delete(file); } }
    }

    [Fact]
    public async Task RapidPinChangesRestoreTheLastPreference()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lucia-companion-pins-{Guid.NewGuid():N}.json");
        try
        {
            var state = new HomeViewModel(SampleHome.Create());
            var session = new HomeProfileSession(state, new LocalHomeStore(file));
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            state.SelectPlace("garden");
            state.TogglePin();
            state.TogglePin();
            state.TogglePin();
            await session.PendingSave;
            var restored = new HomeViewModel(SampleHome.Create());
            await new HomeProfileSession(restored, new LocalHomeStore(file)).InitializeAsync(TestContext.Current.CancellationToken);
            Assert.True(restored.IsPinned("garden"));
        }
        finally { if (File.Exists(file)) { File.Delete(file); } }
    }
}
