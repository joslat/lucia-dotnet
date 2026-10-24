using lucia.Agents.Companion;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using lucia.AgentHost.Companion;

namespace lucia.AgentHost.Auth;

public static class CompanionAuthentication
{
    public const string Scheme = "LuciaCompanion";
    // HomePolicy checks identity/home only. Projections must still filter every place and field through the current grant.
    public const string HomePolicy = "CompanionHome";
    public const string EnginePolicy = "CompanionEngine";
    public const string HistoryPolicy = "CompanionHistory";
    public const string ActivityPolicy = "CompanionActivity";

    public static IServiceCollection AddCompanionAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CompanionOptions>().Bind(configuration.GetSection(CompanionOptions.SectionName))
            .Validate(options => { try { options.Validate(); return true; } catch (ArgumentException) { return false; } },
                "Companion requires a stable HomeId when enabled and bounded ascending credential lifetimes.")
            .ValidateOnStart();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddHostedService<CompanionSessionMaintenance>();
        // Never add this handler to MultiScheme, Administrator or ExternalOrInternal.
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, CompanionAuthenticationHandler>(Scheme, _ => { });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(HomePolicy, policy => Configure(policy, _ => true));
            options.AddPolicy(EnginePolicy, policy => Configure(policy, session => session.Grant.CanReadEngine));
            options.AddPolicy(HistoryPolicy, policy => Configure(policy, session => session.Grant.CanReadHistory));
            options.AddPolicy(ActivityPolicy, policy => Configure(policy, session => session.Grant.CanReadActivity));
        });
        return services;
    }

    public static CompanionSession? Session(HttpContext context) => context.Items.TryGetValue(typeof(CompanionSession), out var value) ? value as CompanionSession : null;

    private static void Configure(AuthorizationPolicyBuilder policy, Func<CompanionSession, bool> permits) => policy
        .AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireAssertion(context =>
            context.Resource is HttpContext http && Session(http) is { } session
            && http.Request.RouteValues["homeId"]?.ToString() == session.Grant.HomeId && permits(session));
}
