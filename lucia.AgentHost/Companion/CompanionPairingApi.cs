using System.Security.Claims;
using lucia.AgentHost.Auth;
using lucia.Agents.Companion;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace lucia.AgentHost.Companion;

public static class CompanionPairingApi
{
    public static WebApplication MapCompanionPairingApi(this WebApplication app)
    {
        var anonymous = app.MapGroup("/api/companion").WithTags("Companion").AllowAnonymous();
        anonymous.MapPost("/pairings", StartAsync).RequireRateLimiting(CompanionRateLimits.Start);
        anonymous.MapPost("/pairings/redeem", RedeemAsync).RequireRateLimiting(CompanionRateLimits.Credentials);
        anonymous.MapPost("/session/renew", RenewAsync).RequireRateLimiting(CompanionRateLimits.Credentials);
        var owner = app.MapGroup("/api/companion/owner").WithTags("Companion management")
            .RequireAuthorization(CompanionAuthentication.OwnerPolicy).RequireRateLimiting(CompanionRateLimits.Owner)
            .AddEndpointFilter<CompanionOwnerFilter>();
        owner.MapGet("/antiforgery", Antiforgery);
        owner.MapPost("/pairings/review", ReviewAsync);
        owner.MapPost("/pairings/{pairingId}/approve", ApproveAsync);
        owner.MapPost("/pairings/{pairingId}/cancel", CancelAsync);
        owner.MapGet("/sessions", async (ICompanionSessionService sessions, IOptions<CompanionOptions> options, HttpContext context) =>
            Results.Json(await sessions.ListActiveAsync(options.Value.HomeId, context.RequestAborted).ConfigureAwait(false), CompanionApiJson.Options));
        owner.MapDelete("/sessions/{sessionId}", async (string sessionId, ICompanionSessionService sessions, IOptions<CompanionOptions> options, HttpContext context) =>
            await sessions.RevokeAsync(sessionId, options.Value.HomeId, context.RequestAborted).ConfigureAwait(false) ? Results.NoContent() : Results.NotFound());
        return app;
    }

    private static async Task<IResult> StartAsync(ICompanionPairingService pairings, HttpContext context)
    {
        var request = await CompanionApiJson.ReadAsync<CompanionPairingStartRequest>(context).ConfigureAwait(false);
        if (request is null) { return Results.BadRequest(new { error = "invalid_device_name" }); }
        try
        {
            var challenge = await pairings.StartAsync(request.DeviceName, context.RequestAborted).ConfigureAwait(false);
            if (challenge is not null) { return Results.Json(challenge, CompanionApiJson.Options); }
            context.Response.Headers.RetryAfter = "60";
            return Results.Json(new { error = "pairing_capacity_reached" }, statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (ArgumentException) { return Results.BadRequest(new { error = "invalid_device_name" }); }
    }

    private static async Task<IResult> RedeemAsync(ICompanionPairingService pairings, HttpContext context)
    {
        var request = await CompanionApiJson.ReadAsync<CompanionPairingRedeemRequest>(context).ConfigureAwait(false);
        if (request is null) { return Results.BadRequest(new { error = "invalid_device_code" }); }
        var result = await pairings.RedeemAsync(request.DeviceCode, context.RequestAborted).ConfigureAwait(false);
        if (result.Status == CompanionPairingStatus.Success) { return Results.Json(result.Tokens, CompanionApiJson.Options); }
        if (result.Status is CompanionPairingStatus.Pending or CompanionPairingStatus.SlowDown) { context.Response.Headers.RetryAfter = "5"; }
        var status = result.Status switch
        {
            CompanionPairingStatus.Pending => StatusCodes.Status202Accepted,
            CompanionPairingStatus.SlowDown => StatusCodes.Status429TooManyRequests,
            CompanionPairingStatus.Denied => StatusCodes.Status403Forbidden,
            CompanionPairingStatus.Expired or CompanionPairingStatus.Consumed => StatusCodes.Status410Gone,
            _ => StatusCodes.Status401Unauthorized
        };
        return Results.Json(new { status = result.Status.ToString().ToLowerInvariant() }, statusCode: status);
    }

    private static async Task<IResult> RenewAsync(ICompanionSessionService sessions, HttpContext context)
    {
        var request = await CompanionApiJson.ReadAsync<CompanionRenewRequest>(context).ConfigureAwait(false);
        if (request is null) { return Results.BadRequest(new { error = "invalid_refresh_credential" }); }
        var tokens = await sessions.RenewAsync(request.RefreshToken, context.RequestAborted).ConfigureAwait(false);
        return tokens is null ? Results.Unauthorized() : Results.Json(tokens, CompanionApiJson.Options);
    }

    private static async Task<IResult> ReviewAsync(ICompanionPairingService pairings, HttpContext context)
    {
        var request = await CompanionApiJson.ReadAsync<CompanionPairingReviewRequest>(context).ConfigureAwait(false);
        if (request is null) { return Results.BadRequest(); }
        var review = await pairings.ReviewAsync(request.UserCode, context.RequestAborted).ConfigureAwait(false);
        return review is null ? Results.NotFound(new { error = "pairing_not_found_or_expired" }) : Results.Json(review, CompanionApiJson.Options);
    }

    private static async Task<IResult> ApproveAsync(string pairingId, ICompanionPairingService pairings, HttpContext context)
    {
        var request = await CompanionApiJson.ReadAsync<CompanionPairingApproveRequest>(context).ConfigureAwait(false);
        if (request?.Grant is null || !request.CodeConfirmed) { return Results.BadRequest(new { error = "confirm_the_code_and_grant" }); }
        try
        {
            return await pairings.ApproveAsync(pairingId, request.Grant, context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, context.RequestAborted).ConfigureAwait(false)
                ? Results.NoContent() : Results.Conflict(new { error = "pairing_expired_or_changed" });
        }
        catch (ArgumentException) { return Results.BadRequest(new { error = "invalid_grant" }); }
    }

    private static async Task<IResult> CancelAsync(string pairingId, ICompanionPairingService pairings, HttpContext context) =>
        await pairings.CancelAsync(pairingId, context.RequestAborted).ConfigureAwait(false) ? Results.NoContent()
            : Results.Conflict(new { error = "pairing_no_longer_pending", message = "Review connected devices to revoke a pairing that was already used." });

    private static IResult Antiforgery(HttpContext context, IAntiforgery antiforgery, IOptions<AntiforgeryOptions> options)
    {
        var tokens = antiforgery.GetTokens(context);
        var name = options.Value.Cookie.Name ?? throw new InvalidOperationException("The antiforgery cookie name is missing.");
        var cookieToken = tokens.CookieToken ?? context.Request.Cookies[name];
        if (string.IsNullOrEmpty(cookieToken)) { throw new InvalidOperationException("The antiforgery cookie token is missing."); }
        // Upgrade an existing shared antiforgery cookie too, without changing captive-setup HTTP defaults globally.
        context.Response.Cookies.Append(name, cookieToken, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });
        return Results.Ok(new { requestToken = tokens.RequestToken, headerName = options.Value.HeaderName ?? "RequestVerificationToken" });
    }
}
