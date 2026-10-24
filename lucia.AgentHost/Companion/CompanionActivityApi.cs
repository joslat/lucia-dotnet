using lucia.AgentHost.Auth;
using lucia.Agents.Companion;
using System.Text.Json;

namespace lucia.AgentHost.Companion;

public static class CompanionActivityApi
{
    public static IServiceCollection AddCompanionActivity(this IServiceCollection services)
    {
        services.AddOptions<CompanionActivityOptions>().BindConfiguration(CompanionActivityOptions.SectionName)
            .Validate(settings => { try { settings.Validate(); return true; } catch (ArgumentException) { return false; } },
                "Activity requires bounded explicit profile/owner-to-member mappings.").ValidateOnStart();
        return services.AddSingleton<CompanionActivityJournal>().AddTransient<CompanionTargetAreaReader>().AddTransient<CompanionActivityProjector>();
    }
    public static WebApplication MapCompanionActivityApi(this WebApplication app)
    {
        app.MapGet("/api/companion/homes/{homeId}/activity", ReadAsync)
            .RequireAuthorization(CompanionAuthentication.ActivityPolicy).RequireRateLimiting(CompanionRateLimits.Credentials)
            .WithTags("Companion");
        return app;
    }

    private static async Task<IResult> ReadAsync(CompanionActivityJournal journal, CompanionActivityProjector projector, ICompanionSessionService sessions, HttpContext context)
    {
        var initial = CompanionAuthentication.Session(context)!;
        var snapshot = await projector.ReadAsync(journal, initial.Grant, context.RequestAborted).ConfigureAwait(false);
        var current = await sessions.AuthenticateAsync(context.Request.Headers.Authorization.ToString()[7..], context.RequestAborted).ConfigureAwait(false);
        if (current is null || current.Id != initial.Id || current.Grant.HomeId != initial.Grant.HomeId || current.Grant.MemberId != initial.Grant.MemberId)
        { return Results.Unauthorized(); }
        if (!current.Grant.CanReadActivity) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        snapshot = CompanionActivityProjector.Restrict(snapshot, current.Grant);
        foreach (var limit in new[] { 6, 3, 1 })
        {
            snapshot = CompanionActivityProjector.LimitAreas(snapshot, limit);
            var json = JsonSerializer.SerializeToUtf8Bytes(snapshot, CompanionApiJson.Options);
            if (json.Length <= 65_536) { return Results.Bytes(json, "application/json"); }
        }
        return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
    }
}
