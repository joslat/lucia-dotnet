using FakeItEasy;
using lucia.Agents.Companion;
using lucia.HomeAssistant.Configuration;
using lucia.HomeAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests.Auth;

public sealed class CompanionTargetAreaReaderTests
{
    [Fact]
    public async Task FreshCanonicalTargetAreasAreQualifiedByExactCurrentPlaceAccessWithoutReadingStates()
    {
        var source = Source();
        var reader = Reader(source);
        var result = await reader.ReadAsync(["climate.porch", "light.inherited", "scene.private"], Grant());
        Assert.Equal("Porch", result.AreasByEntity["climate.porch"].Name);
        Assert.Equal("porch", result.AreasByEntity["light.inherited"].PlaceId);
        Assert.False(result.AreasByEntity.ContainsKey("scene.private"));
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => source.GetEntityStateAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("cycle")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    public async Task AmbiguousOrUnavailableMetadataDoesNotBorrowARoom(string defect)
    {
        var source = Source();
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Returns(defect switch
        {
            "hidden" => [new() { EntityId = "light.target", AreaId = "porch", HiddenBy = "user" }],
            "disabled" => [new() { EntityId = "light.target", AreaId = "porch", DisabledBy = "user" }],
            "cycle" => [new() { EntityId = "light.target", DeviceId = "cycle" }],
            "duplicate" => [new() { EntityId = "light.target", AreaId = "porch" }, new() { EntityId = "light.target", AreaId = "bedroom" }],
            _ => []
        });
        Assert.Empty((await Reader(source).ReadAsync(["light.target"], Grant())).AreasByEntity);
    }

    [Fact]
    public async Task EachReadUsesCurrentRegistryMappingRatherThanTheOldEntityLocationCache()
    {
        var source = Source(); var reader = Reader(source);
        Assert.NotEmpty((await reader.ReadAsync(["climate.porch"], Grant())).AreasByEntity);
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([new() { EntityId = "climate.porch", AreaId = "bedroom" }]);
        Assert.Empty((await reader.ReadAsync(["climate.porch"], Grant())).AreasByEntity);
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task MissingSourceBindingFailsBeforeAnyRegistryRead()
    {
        var source = Source();
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => Reader(source, bound: false).ReadAsync(["climate.porch"], Grant()));
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task NoEligiblePlaceOrTargetDoesNotProbeTheUpstreamSource()
    {
        var source = Source(); var reader = Reader(source);
        Assert.Empty((await reader.ReadAsync([], Grant())).AreasByEntity);
        Assert.Empty((await reader.ReadAsync(["climate.porch"], new("home-a", "member-a", [], canReadActivity: true))).AreasByEntity);
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ARegistryProviderIgnoringCancellationCannotHoldActivityQualificationIndefinitely()
    {
        var source = Source(); var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var blocked = new TaskCompletionSource<lucia.HomeAssistant.Models.AreaRegistryEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).Returns(blocked.Task);
        var pending = Reader(source, clock: clock).ReadAsync(["climate.porch"], Grant());
        Assert.False(pending.IsCompleted); clock.Advance(TimeSpan.FromSeconds(11));
        try { await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5))); }
        finally { blocked.TrySetResult([]); }
    }

    [Fact]
    public async Task ASourceChangeAwayAndBackStillInvalidatesTheTargetAreaRead()
    {
        var source = Source(); var monitor = A.Fake<IOptionsMonitor<HomeAssistantOptions>>();
        var settings = new HomeAssistantOptions { BaseUrl = "http://synthetic-ha.invalid" };
        A.CallTo(() => monitor.CurrentValue).Returns(settings);
        Action<HomeAssistantOptions, string?>? notify = null;
        A.CallTo(() => monitor.OnChange(A<Action<HomeAssistantOptions, string?>>._)).Invokes((Action<HomeAssistantOptions, string?> listener) => notify = listener);
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Invokes(() =>
        {
            settings.BaseUrl = "http://another-source.invalid"; notify!(settings, null);
            settings.BaseUrl = "http://synthetic-ha.invalid"; notify!(settings, null);
        }).Returns([new() { EntityId = "climate.porch", AreaId = "porch" }]);
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => Reader(source, sourceMonitor: monitor).ReadAsync(["climate.porch"], Grant()));
    }

    private static IHomeAssistantClient Source()
    {
        var source = A.Fake<IHomeAssistantClient>();
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).Returns([new() { AreaId = "porch", Name = "Porch" }, new() { AreaId = "bedroom", Name = "PRIVATE_ROOM" }]);
        A.CallTo(() => source.GetDeviceRegistryAsync(A<CancellationToken>._)).Returns([
            new() { Id = "parent", AreaId = "porch" }, new() { Id = "child", ParentDeviceId = "parent" }, new() { Id = "cycle", ParentDeviceId = "cycle" }]);
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([
            new() { EntityId = "climate.porch", AreaId = "porch" }, new() { EntityId = "light.inherited", DeviceId = "child" }, new() { EntityId = "scene.private", AreaId = "bedroom" }]);
        return source;
    }
    private static CompanionGrant Grant() => new("home-a", "member-a", [new("porch", [CompanionField.Temperature])], canReadActivity: true);
    private static CompanionTargetAreaReader Reader(IHomeAssistantClient source, bool bound = true, TimeProvider? clock = null,
        IOptionsMonitor<HomeAssistantOptions>? sourceMonitor = null)
    {
        var settings = Options.Create(new CompanionOptions { Enabled = true, HomeId = "home-a", SourceBaseUrl = bound ? "http://synthetic-ha.invalid" : "" });
        if (sourceMonitor is null)
        {
            sourceMonitor = A.Fake<IOptionsMonitor<HomeAssistantOptions>>();
            A.CallTo(() => sourceMonitor.CurrentValue).Returns(new HomeAssistantOptions { BaseUrl = "http://synthetic-ha.invalid" });
        }
        return new(source, settings, sourceMonitor, clock ?? TimeProvider.System, NullLogger<CompanionTargetAreaReader>.Instance);
    }
}
