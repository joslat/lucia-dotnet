using System.Text.Json;
using lucia.HomeAssistant.Configuration;
using lucia.HomeAssistant.Services;
using lucia.Tests.Helpers;
using lucia.Tests.TestDoubles;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests;

public sealed class HomeAssistantHistoryClientTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(null, null, null)]
    public async Task HistoryFlagsUseHomeAssistantPresenceAndNumericSemantics(bool? minimal, bool? noAttributes, bool? significant)
    {
        using var handler = new FakeHttpMessageHandler(request =>
        {
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            Assert.Equal(minimal == true, query.ContainsKey("minimal_response"));
            Assert.Equal(noAttributes == true, query.ContainsKey("no_attributes"));
            query.TryGetValue("significant_changes_only", out var significantValue);
            Assert.Equal(significant is null ? null : significant.Value ? "1" : "0", significantValue.FirstOrDefault());
            Assert.Equal("light.porch,binary_sensor.door", query["filter_entity_id"]);
            Assert.Equal("2026-10-25T00:00:00+02:00", query["end_time"]);
            Assert.False(query.ContainsKey("skip_initial_state"));
            return new(HttpStatusCode.OK) { Content = new StringContent("[]") };
        });
        using var http = new HttpClient(handler);
        var client = Client(http);
        await client.GetHistoryAsync("2026-10-24T00:00:00+02:00", "light.porch,binary_sensor.door",
            "2026-10-25T00:00:00+02:00", minimal, noAttributes, significant);
    }

    [Fact]
    public async Task NullHistoryIsAnInvalidContractNotSuccessfulEmptyHistory()
    {
        using var handler = new FakeHttpMessageHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent("null") });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<JsonException>(() => Client(http).GetHistoryAsync("2026-10-24T00:00:00Z", "light.porch"));
    }

    [Fact]
    public async Task OversizedHistoryIsRejectedBeforeDeserialization()
    {
        using var handler = new FakeHttpMessageHandler(_ => new(HttpStatusCode.OK)
        { Content = new StringContent("[" + new string(' ', 8 * 1024 * 1024) + "]") });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(http).GetHistoryAsync("2026-10-24T00:00:00Z", "light.porch"));
    }

    [Fact]
    public async Task StreamingHistoryWithoutContentLengthStillHasAByteLimit()
    {
        using var handler = new FakeHttpMessageHandler(_ => new(HttpStatusCode.OK)
        { Content = new UnknownLengthHistoryContent(async (stream, token) => await stream.WriteAsync(new byte[8 * 1024 * 1024 + 1], token)) });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(http).GetHistoryAsync("2026-10-24T00:00:00Z", "light.porch"));
    }

    [Fact]
    public async Task ResponseHeadersReadDoesNotRemoveTheBodyReadTimeout()
    {
        var clock = new FakeTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FakeHttpMessageHandler(_ => new(HttpStatusCode.OK)
        {
            Content = new UnknownLengthHistoryContent(async (_, token) => { entered.TrySetResult(); await release.Task.WaitAsync(token); })
        });
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) };
        var read = Client(http, clock).GetHistoryAsync("2026-10-24T00:00:00Z", "light.porch");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(2));
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(2))); }
        finally { release.TrySetResult(); }
    }

    private static HomeAssistantClient Client(HttpClient http, TimeProvider? clock = null) => new(http, NullLogger<HomeAssistantClient>.Instance,
        new TestOptionsMonitor<HomeAssistantOptions>(new() { BaseUrl = "http://synthetic-ha.invalid" }), clock);
}
