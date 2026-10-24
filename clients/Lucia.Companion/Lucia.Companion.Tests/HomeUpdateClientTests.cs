using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeUpdateClientTests
{
    private const string Access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string Revision = "W/\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"";

    [Fact]
    public async Task InitialReadGetsACompleteValidatedSnapshotAndConditionalReadUsesOnlyItsExactRevision()
    {
        var calls = 0;
        using var api = new CompanionApiClient(new Uri("https://synthetic.invalid"), new ScriptedCompanionHandler((request, _) =>
        {
            Assert.Equal(Access, request.Headers.Authorization!.Parameter);
            Assert.Equal("/api/companion/homes/home-a/snapshot", request.RequestUri!.AbsolutePath);
            Assert.Equal(calls == 0 ? null : Revision, request.Headers.IfNoneMatch.SingleOrDefault()?.ToString());
            var response = new HttpResponseMessage(calls++ == 0 ? HttpStatusCode.OK : HttpStatusCode.NotModified);
            response.Headers.ETag = EntityTagHeaderValue.Parse(Revision);
            if (response.StatusCode == HttpStatusCode.OK) { response.Content = Json(); }
            return Task.FromResult(response);
        }));
        var first = await api.GetHomeUpdateAsync(Access, CompanionHomeClientTests.Grant(), null, TestContext.Current.CancellationToken);
        Assert.NotNull(first.Snapshot);
        Assert.Equal(Revision, first.Revision);
        var next = await api.GetHomeUpdateAsync(Access, CompanionHomeClientTests.Grant(), first.Revision, TestContext.Current.CancellationToken);
        Assert.Null(next.Snapshot);
        Assert.Equal(Revision, next.Revision);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("unconditional-304")]
    [InlineData("mismatched-304")]
    [InlineData("missing-tag")]
    [InlineData("strong-tag")]
    [InlineData("oversized-tag")]
    [InlineData("wrong-home")]
    [InlineData("over-grant")]
    public async Task UnsupportedResponsesNeverConfirmOrReplaceReports(string defect)
    {
        using var api = new CompanionApiClient(new Uri("https://synthetic.invalid"), new ScriptedCompanionHandler((_, _) =>
        {
            var response = new HttpResponseMessage(defect.EndsWith("304", StringComparison.Ordinal) ? HttpStatusCode.NotModified : HttpStatusCode.OK);
            var tag = defect == "mismatched-304" ? Revision.Replace('A', 'B') : defect == "oversized-tag" ? "W/\"" + new string('A', 1000) + "\"" : Revision;
            if (defect != "missing-tag") { response.Headers.ETag = EntityTagHeaderValue.Parse(defect == "strong-tag" ? tag[2..] : tag); }
            var body = CompanionHomeClientTests.Payload();
            if (defect == "wrong-home") { body["homeId"] = "another-home"; }
            if (defect == "over-grant") { body["places"]![0]!["placeId"] = "another-place"; }
            response.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }));
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => api.GetHomeUpdateAsync(Access, CompanionHomeClientTests.Grant(),
            defect == "unconditional-304" ? null : Revision, TestContext.Current.CancellationToken));
        Assert.Equal(ConnectionFailure.InvalidResponse, error.Failure);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"")]
    [InlineData("\"private-marker\"")]
    public async Task InvalidLocalCursorsAreRejectedBeforeAnyRequest(string revision)
    {
        using var api = new CompanionApiClient(new Uri("https://synthetic.invalid"), new ScriptedCompanionHandler((_, _) => throw new InvalidOperationException("Must not send.")));
        await Assert.ThrowsAsync<ArgumentException>(() => api.GetHomeUpdateAsync(Access, CompanionHomeClientTests.Grant(), revision, TestContext.Current.CancellationToken));
    }

    private static StringContent Json() => new(CompanionHomeClientTests.Payload().ToJsonString(), Encoding.UTF8, "application/json");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryAfterIsPreservedWithoutLeakingTheErrorBodyAndAbsoluteDatesUseTheServersDate(bool absoluteDate)
    {
        using var api = new CompanionApiClient(new Uri("https://synthetic.invalid"), new ScriptedCompanionHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("private-throttle-body") };
            var serverNow = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
            response.Headers.Date = serverNow;
            response.Headers.RetryAfter = absoluteDate ? new(serverNow.AddSeconds(95)) : new(TimeSpan.FromSeconds(95));
            return Task.FromResult(response);
        }));
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => api.GetHomeUpdateAsync(Access, CompanionHomeClientTests.Grant(), null, TestContext.Current.CancellationToken));
        Assert.Equal(TimeSpan.FromSeconds(95), error.RetryAfter);
        Assert.DoesNotContain("private-throttle-body", error.ToString());
    }

    [Theory]
    [InlineData("bad-tag")]
    [InlineData("two-tags")]
    [InlineData("lower-case")]
    public async Task MalformedHeadersAlsoFailWithASafeConnectionError(string defect)
    {
        using var api = new CompanionApiClient(new Uri("https://synthetic.invalid"), new ScriptedCompanionHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = Json() };
            response.Headers.TryAddWithoutValidation("ETag", defect == "bad-tag" ? "private-header-marker" : defect == "two-tags"
                ? Revision + ", " + Revision.Replace('A', 'B') : Revision.ToLowerInvariant());
            return Task.FromResult(response);
        }));
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => api.GetHomeUpdateAsync(Access, CompanionHomeClientTests.Grant(), null, TestContext.Current.CancellationToken));
        Assert.Equal(ConnectionFailure.InvalidResponse, error.Failure);
        Assert.DoesNotContain("private-header-marker", error.ToString());
    }
}
