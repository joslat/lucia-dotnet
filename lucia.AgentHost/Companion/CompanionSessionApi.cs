using lucia.AgentHost.Auth;

namespace lucia.AgentHost.Companion;

public static class CompanionSessionApi
{
    public static WebApplication MapCompanionSessionApi(this WebApplication app)
    {
        app.MapGet("/api/companion/homes/{homeId}/session", (HttpContext context) =>
        {
            var session = CompanionAuthentication.Session(context)!;
            return Results.Ok(new
            {
                sessionId = session.Id, session.DeviceName, session.ExpiresAt,
                session.Grant.HomeId, session.Grant.MemberId,
                places = session.Grant.Places.Select(place => new { place.PlaceId, fields = place.Fields.Select(field => field.ToString()) }),
                session.Grant.CanReadActivity, session.Grant.CanReadHistory, session.Grant.CanReadEngine
            });
        }).RequireAuthorization(CompanionAuthentication.HomePolicy).WithTags("Companion");
        return app;
    }
}
