using System.Security.Claims;
using lucia.Agents.Abstractions;
using lucia.Agents.Auth;
using Microsoft.AspNetCore.Antiforgery;

namespace lucia.AgentHost.Companion;

/// <summary>Current owner authority on every management request; cookie mutations also need antiforgery proof.</summary>
public sealed class CompanionOwnerFilter(IApiKeyService keys, IAntiforgery antiforgery, TimeProvider clock) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        if (!await HasCurrentOwnerAsync(context, keys, clock).ConfigureAwait(false))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (!HttpMethods.IsGet(context.Request.Method) && context.User.FindFirstValue("auth_method") != "api_key")
        {
            try { await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false); }
            catch (AntiforgeryValidationException) { return Results.Json(new { error = "antiforgery_required" }, statusCode: StatusCodes.Status403Forbidden); }
        }
        return await next(invocation).ConfigureAwait(false);
    }

    internal static async Task<bool> HasCurrentOwnerAsync(HttpContext context, IApiKeyService keys, TimeProvider clock)
    {
        var keyId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var key = keyId is null ? null : await keys.GetKeyAsync(keyId, context.RequestAborted).ConfigureAwait(false);
        return key is not null && !key.IsRevoked && !(key.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
            && key.Scopes.Contains(AuthOptions.AdministratorScope, StringComparer.Ordinal);
    }
}
