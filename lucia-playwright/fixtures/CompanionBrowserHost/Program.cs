using lucia.Tests.Auth;
using Microsoft.Extensions.FileProviders;

// Synthetic fixture only. Production assets plus production companion auth/API/SQLite services.
// No real AgentHost startup, configuration, owner key, HA, models or household data.
var nativeHome = args is ["--native-home"];
var ownerHome = args is ["--owner-home"];
var dashboard = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../lucia-dashboard/dist"));
if (args.Length == 1 && !nativeHome && !ownerHome) { dashboard = Path.GetFullPath(args[0]); }
if (!nativeHome && !File.Exists(Path.Combine(dashboard, "index.html"))) { throw new InvalidOperationException("Build lucia-dashboard before starting the synthetic browser fixture."); }
using var provider = nativeHome ? null : new PhysicalFileProvider(dashboard);
await using var source = nativeHome || ownerHome ? await SyntheticCompanionHomeSource.StartAsync() : null;
await using var host = await CompanionSecurityTestHost.StartAsync(homeSource: source?.Client,
    sourceBaseUrl: source?.Origin ?? "http://synthetic-ha.invalid", configureApp: app =>
{
    if (provider is null) { return; }
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
    app.MapGet("/api/auth/status", (HttpContext context) => new { authenticated = context.User.Identity?.IsAuthenticated == true, setupComplete = true, hasKeys = true }).AllowAnonymous();
    app.MapFallback((HttpContext context) => context.Request.Path.StartsWithSegments("/api")
        ? Results.NotFound() : Results.File(Path.Combine(dashboard, "index.html"), "text/html"));
});
Console.WriteLine($"LUCIA_BROWSER_FIXTURE_URL={host.Client.BaseAddress}");
// Public test certificate digest, not a credential or a production trust bypass.
Console.WriteLine($"LUCIA_FIXTURE_CERT_SHA256={host.CertificateFingerprint}");
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => { args.Cancel = true; stop.Cancel(); };
try { await Task.WhenAny(Task.Delay(nativeHome || ownerHome ? TimeSpan.FromMinutes(9) : Timeout.InfiniteTimeSpan, stop.Token), WaitForStopAsync(stop.Token)); }
catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
finally { stop.Cancel(); }

static async Task WaitForStopAsync(CancellationToken cancellationToken)
{
    while (await Console.In.ReadLineAsync(cancellationToken) is { } line)
    { if (line == "stop") { return; } }
    // Redirected launchers may close stdin. EOF must not shut down a still-used fixture.
    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
}
