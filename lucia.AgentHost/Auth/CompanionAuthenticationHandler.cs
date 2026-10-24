using System.Security.Claims;
using System.Text.Encodings.Web;
using lucia.Agents.Companion;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace lucia.AgentHost.Auth;

public sealed class CompanionAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, ICompanionSessionService sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers.Authorization;
        if (headers.Count == 0 || !headers.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) { return AuthenticateResult.NoResult(); }
        if (!Request.IsHttps || headers.Count != 1) { return AuthenticateResult.Fail("Companion credentials require HTTPS and a single Authorization header."); }
        var token = headers.ToString()[7..];
        if (!CompanionTokens.HasFormat(token, CompanionTokens.AccessPrefix)) { return AuthenticateResult.Fail("Invalid companion credential."); }
        var session = await sessions.AuthenticateAsync(token, Context.RequestAborted).ConfigureAwait(false);
        if (session is null) { return AuthenticateResult.Fail("Invalid, expired or revoked companion credential."); }
        Context.Items[typeof(CompanionSession)] = session;
        // No broad scope, administrator role, API key, device secret or observation values belong in these claims.
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, session.Grant.MemberId),
            new Claim("companion_session", session.Id), new Claim("companion_home", session.Grant.HomeId)
        ], CompanionAuthentication.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), CompanionAuthentication.Scheme));
    }
}
