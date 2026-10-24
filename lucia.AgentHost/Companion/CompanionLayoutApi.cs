using System.Security.Claims;
using System.Data.Common;
using System.Text.Json;
using lucia.AgentHost.Auth;
using lucia.Agents.Abstractions;
using lucia.Agents.Companion;
using lucia.HomeAssistant.Configuration;
using Microsoft.Extensions.Options;

namespace lucia.AgentHost.Companion;

public static class CompanionLayoutApi
{
    public static WebApplication MapCompanionLayoutApi(this WebApplication app)
    {
        var owner = app.MapGroup("/api/companion/owner/layout").RequireAuthorization(CompanionAuthentication.OwnerPolicy)
            .RequireRateLimiting(CompanionRateLimits.Owner).AddEndpointFilter<CompanionOwnerFilter>().WithTags("Companion management");
        owner.MapGet("", ReadOwnerAsync); owner.MapPut("", WriteOwnerAsync);
        app.MapGet("/api/companion/homes/{homeId}/layout", ReadMemberAsync).RequireAuthorization(CompanionAuthentication.HomePolicy)
            .RequireRateLimiting(CompanionRateLimits.Credentials).WithTags("Companion");
        return app;
    }

    private static async Task<IResult> ReadOwnerAsync(ICompanionLayoutStore store, IOptions<CompanionOptions> options,
        IOptionsMonitor<HomeAssistantOptions> sourceOptions, IApiKeyService keys, TimeProvider clock, HttpContext context)
    {
        CompanionLayoutSnapshot? snapshot = null; var invalid = false;
        try { snapshot = await store.ReadAsync(options.Value.HomeId, context.RequestAborted).ConfigureAwait(false); }
        catch (Exception error) when (error is InvalidDataException or DbException) { invalid = true; }
        if (!await CompanionOwnerFilter.HasCurrentOwnerAsync(context, keys, clock).ConfigureAwait(false)) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        if (invalid) { return Unavailable(); }
        var sourceMatches = SameSource(sourceOptions.CurrentValue.BaseUrl, options.Value.SourceBaseUrl);
        return snapshot is null ? Results.Json(new { error = "layout_not_configured", homeId = options.Value.HomeId,
            sourceBaseUrl = options.Value.SourceBaseUrl, sourceMatches }, CompanionApiJson.Options, statusCode: StatusCodes.Status404NotFound)
            : EncodeOwner(snapshot, options.Value, sourceOptions.CurrentValue.BaseUrl);
    }

    private static async Task<IResult> WriteOwnerAsync(ICompanionLayoutStore store, CompanionHomeReader reader, IOptions<CompanionOptions> options,
        IOptionsMonitor<HomeAssistantOptions> sourceOptions, IApiKeyService keys, TimeProvider clock, HttpContext context)
    {
        CompanionLayoutWriteRequest request;
        try { request = await CompanionLayoutWriteRequest.ReadAsync(context).ConfigureAwait(false); }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge) { return Results.StatusCode(StatusCodes.Status413PayloadTooLarge); }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = "invalid_layout" }); }
        if (request.Layout.HomeId != options.Value.HomeId || !SameSource(request.Layout.SourceBaseUrl, options.Value.SourceBaseUrl))
        { return Results.BadRequest(new { error = "layout_source_mismatch" }); }
        CompanionHomeSnapshot? catalog = null;
        try { catalog = await reader.ReadOwnerCatalogAsync(context.RequestAborted).ConfigureAwait(false); }
        catch (CompanionDataUnavailableException) { /* Recheck authority before even reporting source failure. */ }
        if (!await CompanionOwnerFilter.HasCurrentOwnerAsync(context, keys, clock).ConfigureAwait(false)) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        if (catalog is null || !SameSource(sourceOptions.CurrentValue.BaseUrl, options.Value.SourceBaseUrl)) { return Unavailable(); }
        if (!CompanionLayoutProjector.MatchesCatalog(request.Layout, catalog)) { return Results.BadRequest(new { error = "layout_catalog_changed" }); }
        CompanionLayoutSnapshot? saved = null; var unavailable = false; var capacity = false;
        try { saved = await store.TrySaveAsync(request.Layout, request.ExpectedRevision, context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, context.RequestAborted).ConfigureAwait(false); }
        catch (Exception error) when (error is InvalidDataException or DbException) { unavailable = true; }
        catch (ArgumentException) { capacity = true; }
        if (!await CompanionOwnerFilter.HasCurrentOwnerAsync(context, keys, clock).ConfigureAwait(false)) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        if (unavailable) { return Unavailable(); }
        if (capacity) { return Results.UnprocessableEntity(new { error = "layout_capacity_exceeded" }); }
        return saved is null ? Results.Conflict(new { error = "layout_changed_review_again" }) : EncodeOwner(saved, options.Value, sourceOptions.CurrentValue.BaseUrl);
    }

    private static async Task<IResult> ReadMemberAsync(ICompanionLayoutStore store, CompanionHomeReader reader, ICompanionSessionService sessions,
        IOptions<CompanionOptions> options, IOptionsMonitor<HomeAssistantOptions> sourceOptions, HttpContext context)
    {
        var initial = CompanionAuthentication.Session(context)!;
        CompanionLayoutSnapshot? snapshot = null; CompanionHomeSnapshot? catalog = null; var unavailable = false;
        try
        {
            if (initial.Grant.Places.Any(place => place.Fields.Count > 0))
            {
                snapshot = await store.ReadAsync(initial.Grant.HomeId, context.RequestAborted).ConfigureAwait(false);
                if (snapshot is not null && CompanionLayoutProjector.HasEligiblePlaces(snapshot.Layout, initial.Grant))
                {
                    if (!SameSource(snapshot.Layout.SourceBaseUrl, options.Value.SourceBaseUrl)) { unavailable = true; }
                    else { catalog = await reader.ReadCatalogAsync(initial.Grant, context.RequestAborted).ConfigureAwait(false); }
                }
            }
        }
        catch (Exception error) when (error is InvalidDataException or DbException or CompanionDataUnavailableException) { unavailable = true; }
        var current = await sessions.AuthenticateAsync(context.Request.Headers.Authorization.ToString()[7..], context.RequestAborted).ConfigureAwait(false);
        if (current is null || current.Id != initial.Id || current.Grant.HomeId != initial.Grant.HomeId || current.Grant.MemberId != initial.Grant.MemberId) { return Results.Unauthorized(); }
        if (!current.Grant.Places.Any(place => place.Fields.Count > 0) || snapshot is not null && !CompanionLayoutProjector.HasEligiblePlaces(snapshot.Layout, current.Grant)) { return Missing(); }
        if (unavailable) { return Unavailable(); }
        if (snapshot is null || catalog is null) { return Missing(); }
        if (!SameSource(sourceOptions.CurrentValue.BaseUrl, options.Value.SourceBaseUrl)) { return Unavailable(); }
        var projected = CompanionLayoutProjector.Project(snapshot.Layout, catalog, current);
        return projected is null ? Missing() : Encode(projected, 524_288);
    }

    private static bool SameSource(string first, string second)
    {
        try { return CompanionOptions.ValidateSourceUrl(first) == CompanionOptions.ValidateSourceUrl(second); }
        catch (ArgumentException) { return false; }
    }
    private static IResult EncodeOwner(CompanionLayoutSnapshot snapshot, CompanionOptions options, string currentSource) => Encode(new
    { homeId = options.HomeId, sourceBaseUrl = options.SourceBaseUrl, snapshot.Revision, snapshot.Layout, snapshot.UpdatedAt, snapshot.UpdatedBy,
        sourceMatches = SameSource(snapshot.Layout.SourceBaseUrl, options.SourceBaseUrl) && SameSource(currentSource, options.SourceBaseUrl) }, 1_048_576);
    private static IResult Encode(object result, int maximum)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(result, CompanionApiJson.Options);
        return bytes.Length <= maximum ? Results.Bytes(bytes, "application/json") : Results.UnprocessableEntity(new { error = "layout_projection_too_large" });
    }
    private static IResult Missing() => Results.NotFound(new { error = "layout_not_configured" });
    private static IResult Unavailable() => Results.Json(new { error = "layout_unavailable", message = "Qualified shared layout is unavailable. No empty, cached or sample layout was substituted." }, statusCode: 503);
}
