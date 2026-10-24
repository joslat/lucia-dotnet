using lucia.Agents.Auth;
using Microsoft.AspNetCore.Authentication;

namespace lucia.AgentHost.Auth;

/// <summary>One registration path for production and isolated authentication pipeline tests.</summary>
public static class LuciaAuthenticationExtensions
{
    public static IServiceCollection AddLuciaAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InternalTokenOptions>(configuration.GetSection(InternalTokenOptions.SectionName));
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "MultiScheme";
                options.DefaultChallengeScheme = "MultiScheme";
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthOptions.AuthenticationScheme, _ => { })
            .AddScheme<AuthenticationSchemeOptions, InternalTokenAuthenticationHandler>(InternalTokenDefaults.AuthenticationScheme, _ => { })
            .AddPolicyScheme("MultiScheme", "API Key or Internal Token", options =>
            {
                options.ForwardDefaultSelector = context => context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? InternalTokenDefaults.AuthenticationScheme : AuthOptions.AuthenticationScheme;
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthOptions.AdministratorPolicy, policy => policy.RequireRole(AuthOptions.AdministratorRole));
            options.AddPolicy("InternalOnly", policy => policy.AddAuthenticationSchemes(InternalTokenDefaults.AuthenticationScheme).RequireAuthenticatedUser());
            options.AddPolicy("ExternalOrInternal", policy => policy.AddAuthenticationSchemes(AuthOptions.AuthenticationScheme, InternalTokenDefaults.AuthenticationScheme).RequireAuthenticatedUser());
        });
        services.AddCompanionAuthentication(configuration);
        return services;
    }
}
