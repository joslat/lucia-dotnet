using lucia.Agents.Companion;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.Features;

namespace lucia.AgentHost.Companion;

public sealed class CompanionBoundaryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<CompanionOptions> options)
    {
        if (context.Request.Path.StartsWithSegments("/api/companion"))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            if (!options.Value.Enabled) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            if (!context.Request.IsHttps)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "https_required" }, context.RequestAborted).ConfigureAwait(false);
                return;
            }
            var maximumBodyBytes = HttpMethods.IsPut(context.Request.Method)
                && context.Request.Path.Equals(new PathString("/api/companion/owner/layout"), StringComparison.OrdinalIgnoreCase)
                ? CompanionLayoutWriteRequest.MaximumBodyBytes : 16_384;
            if (context.Request.ContentLength > maximumBodyBytes) { context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge; return; }
            if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodyLimit)
            {
                bodyLimit.MaxRequestBodySize = Math.Min(bodyLimit.MaxRequestBodySize ?? maximumBodyBytes, maximumBodyBytes);
            }
        }
        await next(context).ConfigureAwait(false);
    }
}
