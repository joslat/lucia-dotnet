using System.Text.Json;
using lucia.AgentHost.Auth;
using lucia.Agents.Companion;
using lucia.Agents.Abstractions;

namespace lucia.AgentHost.Companion;

public static class CompanionHomeApi
{
    public static IServiceCollection AddCompanionHomeReads(this IServiceCollection services) => services.AddTransient<CompanionHomeReader>();
    public static WebApplication MapCompanionHomeApi(this WebApplication app)
    {
        var home = app.MapGroup("/api/companion/homes/{homeId}").RequireAuthorization(CompanionAuthentication.HomePolicy)
            .RequireRateLimiting(CompanionRateLimits.Credentials).WithTags("Companion");
        home.MapGet("/snapshot", (CompanionHomeReader reader, ICompanionSessionService sessions, HttpContext context) => ReadAsync(reader, sessions, context, discovery: false));
        home.MapGet("/discovery", (CompanionHomeReader reader, ICompanionSessionService sessions, HttpContext context) => ReadAsync(reader, sessions, context, discovery: true));
        app.MapGet("/api/companion/owner/discovery", ReadOwnerAsync)
            .RequireAuthorization(CompanionAuthentication.OwnerPolicy).RequireRateLimiting(CompanionRateLimits.Owner)
            .AddEndpointFilter<CompanionOwnerFilter>().WithTags("Companion management");
        return app;
    }

    private static async Task<IResult> ReadAsync(CompanionHomeReader reader, ICompanionSessionService sessions, HttpContext context, bool discovery)
    {
        var initial = CompanionAuthentication.Session(context)!;
        CompanionHomeSnapshot? snapshot = null;
        CompanionDataUnavailableException? failure = null;
        try
        {
            snapshot = await reader.ReadAsync(initial.Grant, context.RequestAborted).ConfigureAwait(false);
        }
        catch (CompanionDataUnavailableException error) { failure = error; }
        // Even source availability is private. Recheck authority after successful or failed slow reads.
        var current = await sessions.AuthenticateAsync(context.Request.Headers.Authorization.ToString()[7..], context.RequestAborted).ConfigureAwait(false);
        if (current is null || current.Id != initial.Id || current.Grant.HomeId != initial.Grant.HomeId || current.Grant.MemberId != initial.Grant.MemberId)
        { return Results.Unauthorized(); }
        return failure is not null ? SourceFailure(failure) : Encode(CompanionHomeReader.Restrict(snapshot!, current.Grant), discovery);
    }

    private static async Task<IResult> ReadOwnerAsync(CompanionHomeReader reader, IApiKeyService keys, TimeProvider clock, HttpContext context)
    {
        CompanionHomeSnapshot? snapshot = null;
        CompanionDataUnavailableException? failure = null;
        try
        {
            snapshot = await reader.ReadOwnerCatalogAsync(context.RequestAborted).ConfigureAwait(false);
        }
        catch (CompanionDataUnavailableException error) { failure = error; }
        if (!await CompanionOwnerFilter.HasCurrentOwnerAsync(context, keys, clock).ConfigureAwait(false))
        { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        return failure is not null ? SourceFailure(failure) : Encode(snapshot!, discovery: true);
    }

    private static IResult Encode(CompanionHomeSnapshot snapshot, bool discovery)
    {
        object result = discovery ? new
        {
            snapshot.HomeId, snapshot.CapturedAt,
            places = snapshot.Places.Select(place => new { place.PlaceId, place.Name, place.Sources })
        } : snapshot;
        var json = JsonSerializer.SerializeToUtf8Bytes(result, CompanionApiJson.Options);
        return json.Length <= 524_288 ? Results.Bytes(json, "application/json") : TooLarge();
    }
    private static IResult SourceFailure(CompanionDataUnavailableException error) => error.TooLarge ? TooLarge()
        : Results.Json(new { error = "home_source_unavailable", message = "Qualified home data is unavailable. Check source binding, network and freshness configuration. No cached or sample home was substituted." }, statusCode: 503);
    private static IResult TooLarge() => Results.Json(new { error = "home_projection_too_large", message = "This home projection exceeds its bounded capacity. Narrow the approved access before retrying." }, statusCode: 422);
}
