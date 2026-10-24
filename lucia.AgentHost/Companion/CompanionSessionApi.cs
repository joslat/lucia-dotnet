using lucia.AgentHost.Auth;
using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

public static class CompanionSessionApi
{
    public static WebApplication MapCompanionSessionApi(this WebApplication app)
    {
        app.MapGet("/api/companion/session", Metadata).RequireAuthorization(CompanionAuthentication.OwnSessionPolicy)
            .RequireRateLimiting(CompanionRateLimits.Credentials).WithTags("Companion");
        app.MapPost("/api/companion/session/logout", LogoutAsync).RequireAuthorization(CompanionAuthentication.OwnSessionPolicy)
            .RequireRateLimiting(CompanionRateLimits.Credentials).WithTags("Companion");
        app.MapGet("/api/companion/homes/{homeId}/session", Metadata).RequireAuthorization(CompanionAuthentication.HomePolicy)
            .RequireRateLimiting(CompanionRateLimits.Credentials).WithTags("Companion");
        app.MapPost("/api/companion/homes/{homeId}/session/logout", LogoutAsync).RequireAuthorization(CompanionAuthentication.HomePolicy)
            .RequireRateLimiting(CompanionRateLimits.Credentials).WithTags("Companion");
        return app;
    }

    private static IResult Metadata(HttpContext context)
    {
        var session = CompanionAuthentication.Session(context)!;
        return Results.Ok(new
        {
            sessionId = session.Id, session.DeviceName, session.ExpiresAt,
            session.Grant.HomeId, session.Grant.MemberId,
            places = session.Grant.Places.Select(place => new { place.PlaceId, fields = place.Fields.Select(field => field.ToString()) }),
            session.Grant.CanReadActivity, session.Grant.CanReadHistory, session.Grant.CanReadEngine
        });
    }

    private static async Task<IResult> LogoutAsync(ICompanionSessionService sessions, HttpContext context)
    {
        var session = CompanionAuthentication.Session(context)!;
        await sessions.RevokeAsync(session.Id, session.Grant.HomeId, context.RequestAborted).ConfigureAwait(false);
        return Results.NoContent();
    }
}
