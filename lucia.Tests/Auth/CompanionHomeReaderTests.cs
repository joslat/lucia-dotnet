using FakeItEasy;
using lucia.Agents.Companion;
using lucia.HomeAssistant.Configuration;
using lucia.HomeAssistant.Models;
using lucia.HomeAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests.Auth;

public sealed class CompanionHomeReaderTests
{
    [Fact]
    public async Task DirectAreaOverridesDeviceAndChildDevicesInheritWithoutLeakingRegistryFields()
    {
        var source = A.Fake<IHomeAssistantClient>();
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).Returns([new() { AreaId = "porch", Name = "Porch" }, new() { AreaId = "bedroom", Name = "Private" }]);
        A.CallTo(() => source.GetDeviceRegistryAsync(A<CancellationToken>._)).Returns([
            new() { Id = "parent", AreaId = "porch" }, new() { Id = "child", ParentDeviceId = "parent" },
            new() { Id = "disabled", AreaId = "porch", DisabledBy = "user" }, new() { Id = "cycle", ParentDeviceId = "cycle" }
        ]);
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([
            new() { EntityId = "sensor.direct", AreaId = "bedroom", DeviceId = "parent" },
            new() { EntityId = "sensor.inherited", DeviceId = "child", OriginalName = "Inherited temperature" },
            new() { EntityId = "sensor.hidden", AreaId = "porch", HiddenBy = "user" },
            new() { EntityId = "sensor.disabled", DeviceId = "disabled" }, new() { EntityId = "sensor.cycle", DeviceId = "cycle" },
            new() { EntityId = "sensor.unassigned" }
        ]);
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).Returns(new[] { "direct", "inherited", "hidden", "disabled", "cycle", "unassigned" }
            .Select(name => CompanionHomeApiTests.State("sensor." + name, "21", "temperature", "°C")).ToArray());
        var reader = Reader(source);
        var snapshot = await reader.ReadAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), CancellationToken.None);
        var place = Assert.Single(snapshot.Places);
        Assert.Equal("sensor.inherited", Assert.Single(place.Sources).SourceId);
        Assert.Equal("sensor.inherited", Assert.Single(place.Readings).SourceId);
    }

    [Fact]
    public async Task DuplicateSourceIdsAndAmbiguousDeviceMetadataAreOmittedNotChosenArbitrarily()
    {
        var source = A.Fake<IHomeAssistantClient>();
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).Returns([new() { AreaId = "porch", Name = "Porch" }]);
        A.CallTo(() => source.GetDeviceRegistryAsync(A<CancellationToken>._)).Returns([new() { Id = "device", AreaId = "porch" }, new() { Id = "device", AreaId = "bedroom" }]);
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([
            new() { EntityId = "sensor.duplicate", AreaId = "porch" }, new() { EntityId = "sensor.duplicate", AreaId = "bedroom" },
            new() { EntityId = "sensor.device", DeviceId = "device" }
        ]);
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).Returns([
            CompanionHomeApiTests.State("sensor.duplicate", "21", "temperature", "°C"), CompanionHomeApiTests.State("sensor.device", "21", "temperature", "°C")
        ]);
        var result = await Reader(source).ReadAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), CancellationToken.None);
        Assert.Empty(Assert.Single(result.Places).Sources);
    }

    [Fact]
    public async Task LabelsRemoveFormattingControlsAndBoundEvenTheEntityIdFallback()
    {
        var source = A.Fake<IHomeAssistantClient>();
        var id = "sensor." + new string('a', 240);
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).Returns([new() { AreaId = "porch", Name = "P\u202eorch\n" }]);
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([
            new() { EntityId = id, AreaId = "porch", Name = "\u202e\n" }
        ]);
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).Returns([CompanionHomeApiTests.State(id, "21", "temperature", "°C")]);
        var place = Assert.Single((await Reader(source).ReadAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]))).Places);
        Assert.Equal("Porch", place.Name);
        Assert.Equal(id[..200], Assert.Single(place.Sources).Name);
    }

    [Fact]
    public async Task SourceOriginMustBeExplicitAndMatchBeforeAnyRead()
    {
        var source = A.Fake<IHomeAssistantClient>();
        var mismatch = Reader(source, new() { Enabled = true, HomeId = "home-a", SourceBaseUrl = "http://another-home.invalid" });
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => mismatch.ReadAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])])));
        var missing = Reader(source, new() { Enabled = true, HomeId = "home-a" });
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => missing.ReadAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])])));
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task AProviderThatIgnoresCancellationCannotKeepTheNativeRequestWaitingForever()
    {
        var source = A.Fake<IHomeAssistantClient>();
        var blocked = new TaskCompletionSource<HomeAssistantState[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).Returns(blocked.Task);
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var reading = Reader(source, clock: clock).ReadAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]));
        Assert.False(reading.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(11));
        try { await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => reading.WaitAsync(TimeSpan.FromSeconds(5))); }
        finally { blocked.TrySetResult([]); }
    }

    [Fact]
    public async Task ASourceChangeAwayAndBackDuringAReadStillDiscardsTheResult()
    {
        var source = A.Fake<IHomeAssistantClient>();
        var monitor = A.Fake<IOptionsMonitor<HomeAssistantOptions>>();
        var settings = new HomeAssistantOptions { BaseUrl = "http://synthetic-ha.invalid" };
        A.CallTo(() => monitor.CurrentValue).Returns(settings);
        Action<HomeAssistantOptions, string?>? notify = null;
        A.CallTo(() => monitor.OnChange(A<Action<HomeAssistantOptions, string?>>._))
            .Invokes((Action<HomeAssistantOptions, string?> listener) => notify = listener);
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).Invokes(() =>
        {
            settings.BaseUrl = "http://another-home.invalid";
            notify!(settings, null);
            settings.BaseUrl = "http://synthetic-ha.invalid";
            notify(settings, null);
        }).Returns([]);
        var reader = new CompanionHomeReader(source, Options.Create(new CompanionOptions { Enabled = true, HomeId = "home-a", SourceBaseUrl = settings.BaseUrl }),
            monitor, TimeProvider.System, NullLogger<CompanionHomeReader>.Instance);
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => reader.ReadAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])])));
    }

    [Theory]
    [InlineData("http://ha.invalid", true)]
    [InlineData("https://ha.invalid:8123/", true)]
    [InlineData("http://user:secret@ha.invalid", false)]
    [InlineData("http://ha.invalid/private", false)]
    [InlineData("http://ha.invalid/?token=secret", false)]
    public void SourceBindingAcceptsOnlyOneCredentialFreeOrigin(string value, bool valid)
    {
        if (valid) Assert.NotNull(CompanionOptions.ValidateSourceUrl(value));
        else Assert.Throws<ArgumentException>(() => CompanionOptions.ValidateSourceUrl(value));
    }

    [Fact]
    public void ObservationAndReadBudgetsAreBounded()
    {
        Assert.Throws<ArgumentException>(() => new CompanionOptions { MaximumObservationAge = TimeSpan.Zero }.Validate());
        Assert.Throws<ArgumentException>(() => new CompanionOptions { HomeReadTimeout = TimeSpan.FromMinutes(1) }.Validate());
        Assert.Throws<ArgumentException>(() => new CompanionOptions { MaximumObservationAge = TimeSpan.FromDays(2) }.Validate());
    }

    private static CompanionHomeReader Reader(IHomeAssistantClient source, CompanionOptions? settings = null, TimeProvider? clock = null)
    {
        var monitor = A.Fake<IOptionsMonitor<HomeAssistantOptions>>();
        A.CallTo(() => monitor.CurrentValue).Returns(new HomeAssistantOptions { BaseUrl = "http://synthetic-ha.invalid" });
        return new(source, Options.Create(settings ?? new() { Enabled = true, HomeId = "home-a", SourceBaseUrl = "http://synthetic-ha.invalid" }),
            monitor, clock ?? TimeProvider.System, NullLogger<CompanionHomeReader>.Instance);
    }
}
