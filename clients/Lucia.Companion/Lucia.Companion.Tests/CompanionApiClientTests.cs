using System.Net;
using System.Net.Http.Json;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionApiClientTests
{
    private const string DeviceCode = "lcp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Refresh = "lcr_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Theory]
    [InlineData("http://localhost:5555/")]
    [InlineData("https://owner:secret@localhost/")]
    [InlineData("https://localhost/?token=secret")]
    [InlineData("https://localhost/#secret")]
    [InlineData("https://localhost/other-service/")]
    [InlineData("file:///C:/private")]
    public void EndpointMustBeAnHttpsOriginWithoutCredentials(string address)
    {
        Assert.Throws<ArgumentException>(() => new CompanionApiClient(new Uri(address)));
    }

    [Fact]
    public async Task DeviceSecretStaysInJsonAndPendingDoesNotBecomeCredentials()
    {
        using var client = new CompanionApiClient(new Uri("https://localhost:5555"), new ScriptedCompanionHandler(async (request, cancellationToken) =>
        {
            Assert.Equal("https://localhost:5555/api/companion/pairings/redeem", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("Cookie"));
            Assert.Contains(DeviceCode, await request.Content!.ReadAsStringAsync(cancellationToken));
            var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = JsonContent.Create(new { status = "pending" }) };
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(5));
            return response;
        }));
        var result = await client.PollPairingAsync(DeviceCode, TestContext.Current.CancellationToken);
        Assert.Equal(PairingPollStatus.Pending, result.Status);
        Assert.Null(result.Credentials);
        Assert.Equal(TimeSpan.FromSeconds(5), result.RetryAfter);
    }

    [Fact]
    public async Task RedirectsErrorsAndInvalidCredentialsCannotLeakResponseText()
    {
        foreach (var status in new[] { HttpStatusCode.Redirect, HttpStatusCode.InternalServerError, HttpStatusCode.OK })
        {
            using var client = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((_, _) => Task.FromResult(
                new HttpResponseMessage(status) { Content = new StringContent("sensitive-server-body", System.Text.Encoding.UTF8, "application/json") })));
            var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => client.StartPairingAsync("Phone", TestContext.Current.CancellationToken));
            Assert.DoesNotContain("sensitive-server-body", error.ToString());
        }
        using var malformed = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { sessionId = new string('a', 32), accessToken = "broad-owner-key", accessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5), refreshToken = Refresh, refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(1) }) })));
        await Assert.ThrowsAsync<CompanionConnectionException>(() => malformed.RenewAsync(Refresh, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RenewalIsSentOnceAndSecretsNeverPrint()
    {
        var calls = 0;
        using var client = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((request, _) =>
        {
            calls++;
            Assert.Equal("/api/companion/session/renew", request.RequestUri!.AbsolutePath);
            throw new HttpRequestException("unsafe transport diagnostics " + Refresh);
        }));
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => client.RenewAsync(Refresh, TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
        Assert.DoesNotContain(Refresh, error.ToString());
        var credentials = new DeviceCredentials(new string('a', 32), Access, DateTimeOffset.UtcNow.AddMinutes(5), Refresh, DateTimeOffset.UtcNow.AddDays(1));
        Assert.DoesNotContain(Access, credentials.ToString());
        Assert.DoesNotContain(Refresh, credentials.ToString());
    }

    [Fact]
    public async Task ResponseSizeIsBoundedAndCallerCancellationIsPreserved()
    {
        using var oversized = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 70_000), System.Text.Encoding.UTF8, "application/json") })));
        await Assert.ThrowsAsync<CompanionConnectionException>(() => oversized.StartPairingAsync("Phone", TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        using var client = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new(HttpStatusCode.OK);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.StartPairingAsync("Phone", cancellation.Token));
    }

    [Fact]
    public async Task BodyReadHasItsOwnDeadlineAndMalformedPollStatusIsSafe()
    {
        using var stalled = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new WaitingCompanionContent() })), TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAsync<CompanionConnectionException>(() => stalled.StartPairingAsync("Phone", TestContext.Current.CancellationToken));
        using var malformed = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Gone) { Content = JsonContent.Create(new { status = 123 }) })));
        await Assert.ThrowsAsync<CompanionConnectionException>(() => malformed.PollPairingAsync(DeviceCode, TestContext.Current.CancellationToken));
    }
}
