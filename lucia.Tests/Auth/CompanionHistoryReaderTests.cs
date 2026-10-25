using FakeItEasy;
using lucia.Agents.Companion;
using lucia.HomeAssistant.Configuration;
using lucia.HomeAssistant.Models;
using lucia.HomeAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests.Auth;

public sealed class CompanionHistoryReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = Now.AddDays(-1);
    private static readonly CompanionGrant Grant = new("home-a", "member-a", [new("porch", [CompanionField.Temperature])], canReadHistory: true);

    [Fact]
    public async Task ReadsOnlyAuthorizedSourcesWithFullSamplesAndKeepsBoundarySeparateFromTransitions()
    {
        var source = Source();
        A.CallTo(() => source.GetHistoryAsync(A<string>._, "sensor.porch_temperature", A<string>._, false, false, false, A<CancellationToken>._))
            .Returns([[Sample(Start, "20"), Sample(Start.AddHours(1), "21.5"), Sample(Now, "22")]]);
        var result = await Reader(source).ReadAsync(Grant, Start, Now);
        Assert.Equal("Europe/Zurich", result.TimeZone);
        Assert.Equal(Start, result.Start);
        Assert.Equal(Now, result.End);
        var track = Assert.Single(result.Tracks);
        Assert.Equal("porch", track.CurrentPlaceId); // Current provenance only, not an effective-dated historical assignment.
        Assert.Equal("sensor.porch_temperature", track.SourceId);
        Assert.Equal(2, track.Samples.Count); // Half-open end.
        Assert.True(track.Samples[0].IsBoundary);
        Assert.False(track.Samples[1].IsBoundary);
        Assert.Equal(21.5, track.Samples[1].NumberValue);
        Assert.Equal(CompanionHistoryQuality.Recorded, track.Samples[1].Quality);
        Assert.DoesNotContain("private-marker", System.Text.Json.JsonSerializer.Serialize(result));
        A.CallTo(() => source.GetHistoryAsync(Start.ToString("O"), "sensor.porch_temperature", Now.ToString("O"), false, false, false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task EmptyRecorderResponseKeepsAnEmptyTrackNotAnOffOrContinuousCoverageClaim()
    {
        var result = await Reader(Source()).ReadAsync(Grant, Start, Now);
        Assert.Empty(Assert.Single(result.Tracks).Samples);
    }

    [Theory]
    [InlineData("unknown", CompanionHistoryQuality.Unknown)]
    [InlineData("unavailable", CompanionHistoryQuality.Unavailable)]
    [InlineData("NaN", CompanionHistoryQuality.Unknown)]
    [InlineData("-300", CompanionHistoryQuality.Unknown)]
    public async Task UnknownUnavailableAndInvalidValuesNeverBecomeRecordedValues(string state, CompanionHistoryQuality quality)
    {
        var source = Source();
        History(source, [[Sample(Start.AddHours(1), state)]]);
        var sample = Assert.Single(Assert.Single((await Reader(source).ReadAsync(Grant, Start, Now)).Tracks).Samples);
        Assert.Equal(quality, sample.Quality);
        Assert.Null(sample.NumberValue);
        Assert.Null(sample.ActiveValue);
    }

    [Fact]
    public async Task HistoricalUnitsComeFromHistoricalAttributesNotTheCurrentCatalog()
    {
        var source = Source();
        var state = Sample(Start.AddHours(1), "68");
        state.Attributes["unit_of_measurement"] = "°F";
        History(source, [[state]]);
        var sample = Assert.Single(Assert.Single((await Reader(source).ReadAsync(Grant, Start, Now)).Tracks).Samples);
        Assert.Equal("°F", sample.Unit);
        Assert.Equal(68d, sample.NumberValue);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("mixed")]
    [InlineData("duplicate-track")]
    [InlineData("duplicate-time")]
    [InlineData("before-window")]
    [InlineData("untimed")]
    [InlineData("unordered")]
    public async Task AmbiguousOrUnrequestedHistoryFailsClosed(string kind)
    {
        var source = Source();
        var first = Sample(Start.AddHours(1), "20");
        var second = Sample(Start.AddHours(2), "21");
        switch (kind)
        {
            case "foreign": first.EntityId = "sensor.private_bedroom"; break;
            case "mixed": second.EntityId = "sensor.private_bedroom"; break;
            case "duplicate-time": second.LastUpdated = first.LastUpdated; break;
            case "before-window": first.LastUpdated = Start.AddSeconds(-1).UtcDateTime; break;
            case "untimed": first.LastUpdated = default; first.LastChanged = default; break;
        }
        History(source, kind == "duplicate-track" ? [[first], [second]] : kind == "unordered" ? [[second, first]] : [[first, second]]);
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => Reader(source).ReadAsync(Grant, Start, Now));
    }

    [Fact]
    public async Task HomePermissionDoesNotGrantHistoryAndDenialDoesNotReadTheSource()
    {
        var source = Source();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Reader(source).ReadAsync(new("home-a", "member-a", Grant.Places), Start, Now));
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-2, -1)]
    [InlineData(1, -1)]
    [InlineData(9, 0)]
    [InlineData(32, 31)]
    public async Task InvalidFutureOversizedOrExpiredWindowsDoNotReadSources(int startDaysAgo, int endDaysAgo)
    {
        var source = Source();
        await Assert.ThrowsAsync<ArgumentException>(() => Reader(source).ReadAsync(Grant, Now.AddDays(-startDaysAgo), Now.AddDays(-endDaysAgo)));
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task WrongSourceBindingFailsBeforeReadingAndCancellationRemainsCancellation()
    {
        var source = Source();
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => Reader(source, "http://other-home.invalid").ReadAsync(Grant, Start, Now));
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(source).ReadAsync(Grant, Start, Now, cancellation.Token));
    }

    [Fact]
    public async Task SourceThatIgnoresCancellationStillHasABoundedRead()
    {
        var source = Source();
        var blocked = new TaskCompletionSource<HomeAssistantState[][]>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => source.GetHistoryAsync(A<string>._, A<string?>._, A<string?>._, A<bool?>._, A<bool?>._, A<bool?>._, A<CancellationToken>._))
            .Returns(blocked.Task);
        var clock = new FakeTimeProvider(Now);
        var reading = Reader(source, clock: clock).ReadAsync(Grant, Start, Now);
        Assert.False(reading.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(11));
        try { await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => reading.WaitAsync(TimeSpan.FromSeconds(5))); }
        finally { blocked.TrySetResult([]); }
    }

    [Theory]
    [InlineData("timezone")]
    [InlineData("recorder")]
    [InlineData("history")]
    public async Task MissingHistoryCapabilityOrInvalidCalendarIsNotSuccessfulEmptyHistory(string missing)
    {
        var source = Source();
        A.CallTo(() => source.GetConfigAsync(A<CancellationToken>._)).Returns(CompanionHistoryApiTests.Config() with
        {
            TimeZone = missing == "timezone" ? "not-a-timezone" : "Europe/Zurich",
            Components = new[] { "history", "recorder" }.Where(item => item != missing).ToArray()
        });
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => Reader(source).ReadAsync(Grant, Start, Now));
    }

    [Fact]
    public async Task SourceAndSampleBudgetsRejectTheWholeResultRatherThanSilentlyTruncate()
    {
        var source = Source();
        var ids = Enumerable.Range(0, 257).Select(index => "sensor.temperature_" + index).ToArray();
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Returns(ids.Select(id => new EntityRegistryEntry { EntityId = id, AreaId = "porch" }).ToArray());
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).Returns(ids.Select(id => CompanionHomeApiTests.State(id, "21", "temperature", "°C")).ToArray());
        var oversized = await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => Reader(source).ReadAsync(Grant, Start, Now));
        Assert.True(oversized.TooLarge);
        var samples = Enumerable.Range(0, 50_001).Select(index => Sample(Start.AddSeconds(index), "21")).ToArray();
        source = Source();
        History(source, [samples]);
        oversized = await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => Reader(source).ReadAsync(Grant, Start, Now));
        Assert.True(oversized.TooLarge);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3661)]
    public void LookbackPolicyHasAConfiguredBound(int days) =>
        Assert.Throws<ArgumentException>(() => new CompanionOptions { HistoryLookbackDays = days }.Validate());

    [Fact]
    public async Task ASourceChangeAwayAndBackDuringRecorderReadInvalidatesEverySample()
    {
        var source = Source();
        var monitor = A.Fake<IOptionsMonitor<HomeAssistantOptions>>();
        var current = new HomeAssistantOptions { BaseUrl = "http://synthetic-ha.invalid" };
        var listeners = new List<Action<HomeAssistantOptions, string?>>();
        A.CallTo(() => monitor.CurrentValue).ReturnsLazily(() => current);
        A.CallTo(() => monitor.OnChange(A<Action<HomeAssistantOptions, string?>>._))
            .Invokes((Action<HomeAssistantOptions, string?> callback) => listeners.Add(callback));
        A.CallTo(() => source.GetHistoryAsync(A<string>._, A<string?>._, A<string?>._, A<bool?>._, A<bool?>._, A<bool?>._, A<CancellationToken>._))
            .Invokes(() =>
            {
                current = new() { BaseUrl = "http://other-home.invalid" };
                foreach (var callback in listeners) { callback(current, null); }
                current = new() { BaseUrl = "http://synthetic-ha.invalid" };
                foreach (var callback in listeners) { callback(current, null); }
            }).Returns([[Sample(Start, "20")]]);
        await Assert.ThrowsAsync<CompanionDataUnavailableException>(() => Reader(source, monitorOverride: monitor).ReadAsync(Grant, Start, Now));
    }

    private static void History(IHomeAssistantClient source, HomeAssistantState[][] result) =>
        A.CallTo(() => source.GetHistoryAsync(A<string>._, A<string?>._, A<string?>._, A<bool?>._, A<bool?>._, A<bool?>._, A<CancellationToken>._)).Returns(result);

    private static IHomeAssistantClient Source()
    {
        var source = A.Fake<IHomeAssistantClient>();
        CompanionHomeApiTests.ConfigureHome(source);
        History(source, []);
        A.CallTo(() => source.GetConfigAsync(A<CancellationToken>._)).Returns(new ConfigResponse
        { TimeZone = "Europe/Zurich", Components = ["history", "recorder"], ConfigDir = "/private-marker", Elevation = 0, Latitude = 0, Longitude = 0,
            LocationName = "private-marker", UnitSystem = null!, Version = "synthetic" });
        return source;
    }

    private static HomeAssistantState Sample(DateTimeOffset at, string value)
    {
        var state = CompanionHomeApiTests.State("sensor.porch_temperature", value, "temperature", "°C");
        state.LastChanged = at.UtcDateTime;
        state.LastUpdated = at.UtcDateTime;
        state.LastReported = Now; // Must never date recorder evidence using the current report heartbeat.
        return state;
    }

    private static CompanionHistoryReader Reader(IHomeAssistantClient source, string binding = "http://synthetic-ha.invalid", TimeProvider? clock = null,
        IOptionsMonitor<HomeAssistantOptions>? monitorOverride = null)
    {
        var monitor = monitorOverride ?? A.Fake<IOptionsMonitor<HomeAssistantOptions>>();
        if (monitorOverride is null) { A.CallTo(() => monitor.CurrentValue).Returns(new HomeAssistantOptions { BaseUrl = "http://synthetic-ha.invalid" }); }
        var options = Options.Create(new CompanionOptions { Enabled = true, HomeId = "home-a", SourceBaseUrl = binding });
        clock ??= new FakeTimeProvider(Now);
        var catalog = new CompanionHomeReader(source, options, monitor, clock, NullLogger<CompanionHomeReader>.Instance);
        return new(source, catalog, options, monitor, clock, NullLogger<CompanionHistoryReader>.Instance);
    }
}
