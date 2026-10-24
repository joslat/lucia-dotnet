using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using FakeItEasy;
using lucia.HomeAssistant.Configuration;
using lucia.HomeAssistant.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace lucia.Tests.Auth;

/// <summary>Only synthetic read responses, on loopback. Unknown commands fail; no service/action endpoint exists.</summary>
public sealed class SyntheticCompanionHomeSource : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient http;
    public string Origin { get; }
    public HomeAssistantClient Client { get; }
    public ConcurrentBag<string> Reads { get; } = [];
    public DateTimeOffset ReportedAt { get; } = DateTimeOffset.UtcNow.AddSeconds(-1);
    private const string Token = "synthetic-protocol-proof-only";

    private SyntheticCompanionHomeSource(WebApplication app, string origin)
    {
        this.app = app;
        Origin = origin;
        var monitor = A.Fake<IOptionsMonitor<HomeAssistantOptions>>();
        A.CallTo(() => monitor.CurrentValue).Returns(new HomeAssistantOptions { BaseUrl = origin, AccessToken = Token, TimeoutSeconds = 5 });
        http = new(new HomeAssistantAuthorizationHandler(monitor) { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false } });
        Client = new(http, NullLogger<HomeAssistantClient>.Instance, monitor);
    }

    public static async Task<SyntheticCompanionHomeSource> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        app.UseWebSockets();
        SyntheticCompanionHomeSource fixture = null!;
        app.MapGet("/api/states", (HttpContext context) =>
        {
            if (context.Request.Headers.Authorization != "Bearer " + Token) { return Results.Unauthorized(); }
            fixture.Reads.Add("states");
            return Results.Json(new[] {
                new { entity_id = "sensor.porch_temperature", state = "21.5", attributes = new { device_class = "temperature", unit_of_measurement = "°C", private_attribute = "private-wire-marker" },
                    last_changed = fixture.ReportedAt.AddDays(-4), last_updated = fixture.ReportedAt.AddDays(-4), last_reported = fixture.ReportedAt,
                    context = new { user_id = "private-wire-marker" } },
                new { entity_id = "sensor.bedroom_temperature", state = "18", attributes = new { device_class = "temperature", unit_of_measurement = "°C", private_attribute = "private-wire-marker" },
                    last_changed = fixture.ReportedAt, last_updated = fixture.ReportedAt, last_reported = fixture.ReportedAt,
                    context = new { user_id = "private-wire-marker" } }
            });
        });
        app.MapGet("/api/websocket", (HttpContext context) => fixture.HandleSocketAsync(context));
        await app.StartAsync();
        var origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        fixture = new(app, origin);
        return fixture;
    }

    private async Task HandleSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var ct = context.RequestAborted;
        await WriteAsync(socket, new { type = "auth_required", ha_version = "synthetic-not-a-real-host" }, ct);
        using var authentication = await ReadAsync(socket, ct);
        if (authentication.RootElement.GetProperty("type").GetString() != "auth"
            || authentication.RootElement.GetProperty("access_token").GetString() != Token)
        { await WriteAsync(socket, new { type = "auth_invalid", message = "synthetic authorization required" }, ct); return; }
        await WriteAsync(socket, new { type = "auth_ok" }, ct);
        using var command = await ReadAsync(socket, ct);
        var type = command.RootElement.GetProperty("type").GetString()!;
        var json = type switch
        {
            "config/area_registry/list" => """[{"area_id":"porch","name":"Porch"},{"area_id":"bedroom","name":"private-wire-bedroom"}]""",
            "config/entity_registry/list" => """[{"entity_id":"sensor.porch_temperature","device_id":"child","original_name":"Porch temperature"},{"entity_id":"sensor.bedroom_temperature","area_id":"bedroom"}]""",
            "config/device_registry/list" => """[{"id":"parent","area_id":"porch","connections":[["mac","private-wire-marker"]]},{"id":"child","parent_device_id":"parent","area_id":null}]""",
            _ => throw new InvalidOperationException("Unexpected synthetic source command")
        };
        Reads.Add(type);
        using var result = JsonDocument.Parse(json);
        await WriteAsync(socket, new { id = command.RootElement.GetProperty("id").GetInt32(), type = "result", success = true, result = result.RootElement }, ct);
        // Complete the ordinary client close handshake rather than timing out during disposal.
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct);
    }

    private static async Task<JsonDocument> ReadAsync(WebSocket socket, CancellationToken ct)
    {
        await using var message = WebSocketStream.CreateReadableMessageStream(socket);
        return await JsonDocument.ParseAsync(message, cancellationToken: ct);
    }
    private static Task WriteAsync(WebSocket socket, object message, CancellationToken ct) =>
        socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message).AsMemory(), WebSocketMessageType.Text, true, ct).AsTask();

    public async ValueTask DisposeAsync()
    {
        http.Dispose();
        await app.DisposeAsync();
    }
}
