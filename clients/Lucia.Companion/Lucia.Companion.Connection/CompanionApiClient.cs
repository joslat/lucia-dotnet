using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lucia.Companion.Core;

namespace Lucia.Companion.Connection;

/// <summary>One fixed HTTPS origin. No ambient authority, redirects, automatic retries, or raw diagnostic bodies.</summary>
public sealed class CompanionApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter<ObservationRole>(allowIntegerValues: false), new JsonStringEnumConverter<ObservationQuality>(allowIntegerValues: false) }
    };
    private readonly HttpClient client;
    private readonly TimeSpan bodyTimeout;
    public Uri Endpoint { get; }

    public CompanionApiClient(Uri endpoint) : this(endpoint, new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false }) { }

    // Only test assemblies can provide a handler. Production never installs a certificate-validation bypass.
    internal CompanionApiClient(Uri endpoint, HttpMessageHandler handler, TimeSpan? bodyTimeout = null)
    {
        try { Endpoint = CompanionEndpoint.Validate(endpoint); }
        catch { handler.Dispose(); throw; }
        this.bodyTimeout = bodyTimeout ?? TimeSpan.FromSeconds(30);
        client = new HttpClient(handler) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<DevicePairingChallenge> StartPairingAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        using var request = Post("pairings", new { deviceName = WireValue.DeviceName(deviceName) });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        Require(response, HttpStatusCode.OK);
        return await ReadAsync<DevicePairingChallenge>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PairingPoll> PollPairingAsync(string deviceCode, CancellationToken cancellationToken = default)
    {
        WireValue.Secret(deviceCode, "lcp_");
        using var request = Post("pairings/redeem", new { deviceCode });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.OK)
        { return new(PairingPollStatus.Success, TimeSpan.Zero, await ReadAsync<DeviceCredentials>(response, cancellationToken).ConfigureAwait(false)); }
        var state = response.StatusCode switch
        {
            HttpStatusCode.Accepted => PairingPollStatus.Pending,
            HttpStatusCode.TooManyRequests => PairingPollStatus.SlowDown,
            HttpStatusCode.Forbidden => PairingPollStatus.Denied,
            HttpStatusCode.Unauthorized => PairingPollStatus.Invalid,
            HttpStatusCode.Gone => PairingPollStatus.Expired,
            _ => throw Failure(response)
        };
        // Consumed is separate from expiry: the server may have issued credentials whose delivery was lost.
        if (response.StatusCode == HttpStatusCode.Gone)
        {
            var body = await ReadAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);
            if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String
                || status.GetString() is not ("consumed" or "expired"))
            { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
            if (status.GetString() == "consumed") { state = PairingPollStatus.Consumed; }
        }
        var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
        return new(state, TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 5, 300)));
    }

    public async Task<DeviceCredentials> RenewAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        WireValue.Secret(refreshToken, "lcr_");
        using var request = Post("session/renew", new { refreshToken });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        Require(response, HttpStatusCode.OK);
        return await ReadAsync<DeviceCredentials>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeviceSession> GetSessionAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = Authorized(HttpMethod.Get, "session", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        Require(response, HttpStatusCode.OK);
        return await ReadAsync<DeviceSession>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = Authorized(HttpMethod.Post, "session/logout", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        Require(response, HttpStatusCode.NoContent);
    }

    public async Task<HomeSnapshot> GetHomeAsync(string accessToken, DeviceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        using var request = Authorized(HttpMethod.Get, $"homes/{session.HomeId}/snapshot", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        Require(response, HttpStatusCode.OK);
        var snapshot = await ReadAsync<HomeSnapshot>(response, cancellationToken, 524_288).ConfigureAwait(false);
        snapshot.RequireGrant(session);
        return snapshot;
    }

    public async Task<HomeCatalog> GetDiscoveryAsync(string accessToken, DeviceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        using var request = Authorized(HttpMethod.Get, $"homes/{session.HomeId}/discovery", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        Require(response, HttpStatusCode.OK);
        var catalog = await ReadAsync<HomeCatalog>(response, cancellationToken, 524_288).ConfigureAwait(false);
        catalog.RequireGrant(session);
        return catalog;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        WireValue.Secret(refreshToken, "lcr_");
        using var request = Post("session/revoke", new { refreshToken });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        Require(response, HttpStatusCode.NoContent);
    }

    private static HttpRequestMessage Post(string path, object body) => new(HttpMethod.Post, "/api/companion/" + path) { Content = JsonContent.Create(body, options: JsonOptions) };
    private static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken)
    {
        WireValue.Secret(accessToken, "lca_");
        var request = new HttpRequestMessage(method, "/api/companion/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IOException)
        { throw new CompanionConnectionException(ConnectionFailure.Transport); }
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken, long maximumBytes = 65_536)
    {
        // HttpClient's ResponseHeadersRead timeout ends at the headers. Content needs its own deadline.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(bodyTimeout);
        try
        {
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)) { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
            await response.Content.LoadIntoBufferAsync(maximumBytes, deadline.Token).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, deadline.Token).ConfigureAwait(false)
                ?? throw new CompanionConnectionException(ConnectionFailure.InvalidResponse);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is JsonException or ArgumentException or HttpRequestException or IOException or OperationCanceledException)
        { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
    }

    private static void Require(HttpResponseMessage response, HttpStatusCode expected)
    { if (response.StatusCode != expected) { throw Failure(response); } }
    private static CompanionConnectionException Failure(HttpResponseMessage response) => new(
        response.StatusCode == HttpStatusCode.Unauthorized ? ConnectionFailure.Unauthorized : ConnectionFailure.Rejected, response.StatusCode);
    public void Dispose() => client.Dispose();
}
