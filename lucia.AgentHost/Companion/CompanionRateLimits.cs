using System.Globalization;
using System.Threading.RateLimiting;

namespace lucia.AgentHost.Companion;

public static class CompanionRateLimits
{
    public const string Start = "CompanionPairingStart";
    public const string Credentials = "CompanionCredentials";
    public const string Owner = "CompanionOwner";

    public static IServiceCollection AddCompanionRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? Math.Max(1, (int)Math.Ceiling(after.TotalSeconds)) : 60;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            options.AddPolicy(Start, context => Window(context, 5));
            options.AddPolicy(Credentials, context => Window(context, 120));
            options.AddPolicy(Owner, context => Window(context, 60));
        });
        return services;
    }

    private static RateLimitPartition<string> Window(HttpContext context, int limit) => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        });
}
