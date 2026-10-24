using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class LocalHomeStoreTests
{
    [Fact]
    public async Task RoundTripStoresConfigurationButNeverSensorReadings()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"lucia-companion-test-{Guid.NewGuid():N}");
        var file = Path.Combine(folder, "home.json");
        try
        {
            var store = new LocalHomeStore(file);
            var home = SampleHome.Create();
            await store.SaveAsync(HomeProfile.From(home, ["garden"]), null, TestContext.Current.CancellationToken);
            var json = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
            Assert.DoesNotContain("21.4", json);
            Assert.DoesNotContain("observations", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Water leak", json);
            var profile = await store.LoadAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(profile);
            Assert.Equal("garden", Assert.Single(profile.PinnedPlaceIds));
            var restored = profile.ApplyTo(home);
            Assert.Equal(home.Places.Select(place => place.Id), restored.Places.Select(place => place.Id));
            Assert.NotEmpty(restored.Places[0].Observations);
            Assert.Equal(2, Directory.GetFiles(folder).Length);
            Assert.True(File.Exists(file + ".lock"));
        }
        finally
        {
            if (File.Exists(file)) { File.Delete(file); }
            if (File.Exists(file + ".lock")) { File.Delete(file + ".lock"); }
            if (Directory.Exists(folder)) { Directory.Delete(folder); }
        }
    }

    [Fact]
    public async Task CorruptProfileIsNotSilentlyReplaced()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lucia-companion-corrupt-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(file, "not valid json", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new LocalHomeStore(file).LoadAsync(TestContext.Current.CancellationToken));
            Assert.Equal("not valid json", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        }
        finally { if (File.Exists(file)) { File.Delete(file); } }
    }

    [Fact]
    public void ProfileCannotSwitchHouseholdOrSchema()
    {
        var home = SampleHome.Create();
        var profile = HomeProfile.From(home, []);
        Assert.Throws<ArgumentException>(() => (profile with { HomeId = "another-home" }).ApplyTo(home));
        Assert.Throws<ArgumentException>(() => (profile with { SchemaVersion = 99 }).ApplyTo(home));
    }

    [Fact]
    public async Task MissingProfileHasNoSideEffects()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lucia-companion-absent-{Guid.NewGuid():N}.json");
        Assert.Null(await new LocalHomeStore(file).LoadAsync(TestContext.Current.CancellationToken));
        Assert.False(File.Exists(file));
    }
}
