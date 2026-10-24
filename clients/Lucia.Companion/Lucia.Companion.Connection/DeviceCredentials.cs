namespace Lucia.Companion.Connection;

public sealed class DeviceCredentials
{
    public DeviceCredentials(string sessionId, string accessToken, DateTimeOffset accessExpiresAt, string refreshToken, DateTimeOffset refreshExpiresAt)
    {
        SessionId = WireValue.Identifier(sessionId);
        AccessToken = WireValue.Secret(accessToken, "lca_");
        RefreshToken = WireValue.Secret(refreshToken, "lcr_");
        if (accessExpiresAt <= DateTimeOffset.UnixEpoch || refreshExpiresAt < accessExpiresAt)
        { throw new ArgumentException("Credential expiry is invalid."); }
        AccessExpiresAt = accessExpiresAt;
        RefreshExpiresAt = refreshExpiresAt;
    }
    public string SessionId { get; }
    public string AccessToken { get; }
    public DateTimeOffset AccessExpiresAt { get; }
    public string RefreshToken { get; }
    public DateTimeOffset RefreshExpiresAt { get; }
    public override string ToString() => "Device credentials redacted";
}
