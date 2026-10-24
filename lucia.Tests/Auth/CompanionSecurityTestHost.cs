using System.Data.Common;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FakeItEasy;
using lucia.AgentHost.Auth;
using lucia.AgentHost.Companion;
using lucia.Agents.Abstractions;
using lucia.Agents.Auth;
using lucia.Agents.Companion;
using lucia.Data.Companion;
using lucia.Tests.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using lucia.HomeAssistant.Services;
using lucia.HomeAssistant.Configuration;
using Microsoft.Extensions.Options;

namespace lucia.Tests.Auth;

/// <summary>Loopback HTTPS only. No AgentHost startup, user secrets, models or household fixtures. HA is fake unless an explicit synthetic source is supplied.</summary>
public sealed class CompanionSecurityTestHost : IAsyncDisposable
{
    private readonly SqliteTestHelper database;
    private readonly WebApplication app;
    private readonly X509Certificate2 certificate;
    public HttpClient Client { get; }
    public ICompanionSessionService Sessions { get; }
    public ICompanionPairingService Pairings { get; }
    public IApiKeyService Keys { get; }
    public IHomeAssistantClient HomeAssistant { get; }
    public HomeAssistantOptions SourceSettings => app.Services.GetRequiredService<IOptionsMonitor<HomeAssistantOptions>>().CurrentValue;

    public Lucia.Companion.Connection.CompanionApiClient CreateNativeClient()
    {
        var fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        return new(Client.BaseAddress!, new HttpClientHandler
        {
            UseCookies = false, AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.GetCertHashString(HashAlgorithmName.SHA256) == fingerprint
        });
    }

    private CompanionSecurityTestHost(SqliteTestHelper database, WebApplication app, X509Certificate2 certificate,
        HttpClient client, ICompanionSessionService sessions, ICompanionPairingService pairings, IApiKeyService keys, IHomeAssistantClient homeAssistant)
    {
        this.database = database; this.app = app; this.certificate = certificate; Client = client; Sessions = sessions; Pairings = pairings; Keys = keys;
        HomeAssistant = homeAssistant;
    }

    public static async Task<CompanionSecurityTestHost> StartAsync(TimeProvider? clock = null, Action<WebApplication>? configureApp = null,
        IHomeAssistantClient? homeSource = null, string sourceBaseUrl = "http://synthetic-ha.invalid")
    {
        clock ??= TimeProvider.System;
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10));
        // Windows Schannel requires a temporary imported key container. No trust-store installation or PersistKeySet.
        var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null,
            OperatingSystem.IsWindows() ? X509KeyStorageFlags.UserKeySet : X509KeyStorageFlags.EphemeralKeySet);
        var database = new SqliteTestHelper();
        using (var connection = database.ConnectionFactory.CreateConnection())
        using (var command = connection.CreateCommand()) { command.CommandText = CompanionSchema.Sql + CompanionPairingSchema.Sql; command.ExecuteNonQuery(); }
        var configuration = new Dictionary<string, string?>
        {
            ["Companion:Enabled"] = "true", ["Companion:HomeId"] = "home-a", ["InternalAuth:Token"] = "synthetic-internal",
            ["Companion:SourceBaseUrl"] = sourceBaseUrl
        };
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        var keys = A.Fake<IApiKeyService>();
        var owner = new ApiKeyEntry { Id = "owner-a", Name = "Synthetic owner", KeyHash = "unused", KeyPrefix = "unused", Scopes = [AuthOptions.AdministratorScope] };
        A.CallTo(() => keys.ValidateKeyAsync("synthetic-owner", A<CancellationToken>._)).Returns(owner);
        A.CallTo(() => keys.GetKeyAsync("owner-a", A<CancellationToken>._)).Returns(new ApiKeySummary
        {
            Id = "owner-a", Name = "Synthetic owner", KeyPrefix = "unused", CreatedAt = DateTime.UtcNow, Scopes = [AuthOptions.AdministratorScope],
            LastUsedAt = null, ExpiresAt = null, IsRevoked = false, RevokedAt = null
        });
        var cookies = A.Fake<ISessionService>();
        A.CallTo(() => cookies.ValidateSession("synthetic-cookie")).Returns([
            new Claim(ClaimTypes.NameIdentifier, "owner-a"), new Claim(ClaimTypes.Role, AuthOptions.AdministratorRole)]);
        builder.Services.AddSingleton(keys);
        builder.Services.AddSingleton(cookies);
        var homeAssistant = homeSource ?? A.Fake<IHomeAssistantClient>();
        builder.Services.AddSingleton(homeAssistant);
        builder.Services.Configure<HomeAssistantOptions>(settings => settings.BaseUrl = sourceBaseUrl);
        builder.Services.AddSingleton<TimeProvider>(clock);
        var sessions = new RelationalCompanionSessionService(_ => ValueTask.FromResult<DbConnection>(database.ConnectionFactory.CreateConnection()),
            new() { Enabled = true, HomeId = "home-a" }, clock);
        builder.Services.AddSingleton<ICompanionSessionService>(sessions);
        var pairings = new RelationalCompanionPairingService(_ => ValueTask.FromResult<DbConnection>(database.ConnectionFactory.CreateConnection()),
            new() { Enabled = true, HomeId = "home-a" }, clock);
        builder.Services.AddSingleton<ICompanionPairingService>(pairings);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddLuciaAuthentication(builder.Configuration);
        builder.Services.AddCompanionHomeReads();
        var app = builder.Build();
        app.UseMiddleware<CompanionBoundaryMiddleware>();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/general", () => "general").RequireAuthorization();
        app.MapGet("/owner", () => "owner").RequireAuthorization(AuthOptions.AdministratorPolicy);
        app.MapGet("/internal", () => "internal").RequireAuthorization("InternalOnly");
        app.MapGet("/engine/{homeId}", () => "engine").RequireAuthorization(CompanionAuthentication.EnginePolicy);
        app.MapGet("/history/{homeId}", () => "history").RequireAuthorization(CompanionAuthentication.HistoryPolicy);
        app.MapGet("/activity/{homeId}", () => "activity").RequireAuthorization(CompanionAuthentication.ActivityPolicy);
        app.MapCompanionSessionApi();
        app.MapCompanionPairingApi();
        app.MapCompanionHomeApi();
        configureApp?.Invoke(app);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        var handler = new HttpClientHandler
        {
            UseCookies = false, AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.GetCertHashString(HashAlgorithmName.SHA256) == fingerprint
        };
        return new(database, app, certificate, new HttpClient(handler) { BaseAddress = new Uri(address) }, sessions, pairings, keys, homeAssistant);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
        certificate.Dispose();
        database.Dispose();
    }
}
