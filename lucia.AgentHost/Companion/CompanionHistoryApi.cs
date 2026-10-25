using System.Globalization;
using System.Text.Json;
using lucia.AgentHost.Auth;
using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

public static class CompanionHistoryApi
{
    public static IServiceCollection AddCompanionHistory(this IServiceCollection services) => services.AddTransient<CompanionHistoryReader>();

    public static WebApplication MapCompanionHistoryApi(this WebApplication app)
    {
        app.MapGet("/api/companion/homes/{homeId}/history", ReadAsync)
            .RequireAuthorization(CompanionAuthentication.HistoryPolicy).RequireRateLimiting(CompanionRateLimits.Credentials)
            .WithTags("Companion");
        return app;
    }

    private static async Task<IResult> ReadAsync(CompanionHistoryReader reader, ICompanionSessionService sessions, HttpContext context)
    {
        if (!Timestamp(context.Request, "start", out var start) || !Timestamp(context.Request, "end", out var end))
        { return InvalidWindow(); }
        var initial = CompanionAuthentication.Session(context)!;
        CompanionHistorySnapshot? snapshot = null;
        CompanionDataUnavailableException? failure = null;
        try { snapshot = await reader.ReadAsync(initial.Grant, start, end, context.RequestAborted).ConfigureAwait(false); }
        catch (ArgumentException) { return InvalidWindow(); }
        catch (CompanionDataUnavailableException error) { failure = error; }
        // Recheck identity/capability even after failed reads. Source availability is not public information.
        var current = await sessions.AuthenticateAsync(context.Request.Headers.Authorization.ToString()[7..], context.RequestAborted).ConfigureAwait(false);
        if (current is null || current.Id != initial.Id || current.Grant.HomeId != initial.Grant.HomeId || current.Grant.MemberId != initial.Grant.MemberId)
        { return Results.Unauthorized(); }
        if (!current.Grant.CanReadHistory) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        if (failure is not null)
        {
            return Results.Json(new { error = failure.TooLarge ? "history_projection_too_large" : "history_source_unavailable" },
                statusCode: failure.TooLarge ? 422 : 503);
        }
        var json = JsonSerializer.SerializeToUtf8Bytes(CompanionHistoryReader.Restrict(snapshot!, current.Grant), CompanionApiJson.Options);
        return json.Length <= 4 * 1024 * 1024 ? Results.Bytes(json, "application/json")
            : Results.Json(new { error = "history_projection_too_large" }, statusCode: 422);
    }

    private static bool Timestamp(HttpRequest request, string key, out DateTimeOffset instant)
    {
        instant = default;
        if (!request.Query.TryGetValue(key, out var values) || values.Count != 1 || values[0] is not { Length: >= 20 and <= 40 } value)
        { return false; }
        // Offset-less values must never acquire the server machine's local timezone.
        return DateTimeOffset.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out instant);
    }

    private static IResult InvalidWindow() => Results.Json(new { error = "invalid_history_window" }, statusCode: 400);
}
