namespace lucia.Agents.Companion;

/// <summary>Returned once to the device. Never log, put in URLs, or save outside platform secure storage.</summary>
public sealed class CompanionSessionTokens(string sessionId, string accessToken, DateTimeOffset accessExpiresAt,
    string refreshToken, DateTimeOffset refreshExpiresAt)
{
    public string SessionId { get; } = sessionId;
    public string AccessToken { get; } = accessToken;
    public DateTimeOffset AccessExpiresAt { get; } = accessExpiresAt;
    public string RefreshToken { get; } = refreshToken;
    public DateTimeOffset RefreshExpiresAt { get; } = refreshExpiresAt;
    public override string ToString() => "Companion credentials redacted";
}
