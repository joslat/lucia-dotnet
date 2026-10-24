using lucia.Tests.Auth;
using Microsoft.Extensions.FileProviders;

// Synthetic fixture only. Production assets plus production companion auth/API/SQLite services.
// No real AgentHost startup, configuration, owner key, HA, models or household data.
var dashboard = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../lucia-dashboard/dist"));
if (args.Length == 1) { dashboard = Path.GetFullPath(args[0]); }
if (!File.Exists(Path.Combine(dashboard, "index.html"))) { throw new InvalidOperationException("Build lucia-dashboard before starting the synthetic browser fixture."); }
using var provider = new PhysicalFileProvider(dashboard);
await using var host = await CompanionSecurityTestHost.StartAsync(configureApp: app =>
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
    app.MapGet("/api/auth/status", (HttpContext context) => new { authenticated = context.User.Identity?.IsAuthenticated == true, setupComplete = true, hasKeys = true }).AllowAnonymous();
    app.MapFallback((HttpContext context) => context.Request.Path.StartsWithSegments("/api")
        ? Results.NotFound() : Results.File(Path.Combine(dashboard, "index.html"), "text/html"));
});
Console.WriteLine($"LUCIA_BROWSER_FIXTURE_URL={host.Client.BaseAddress}");
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => { args.Cancel = true; stop.Cancel(); };
try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); }
catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
