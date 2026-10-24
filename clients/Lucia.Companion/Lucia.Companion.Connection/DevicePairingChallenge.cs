namespace Lucia.Companion.Connection;

public sealed class DevicePairingChallenge
{
    public DevicePairingChallenge(string deviceCode, string userCode, DateTimeOffset expiresAt, int pollIntervalSeconds)
    {
        DeviceCode = WireValue.Secret(deviceCode, "lcp_");
        if (userCode is null || userCode.Length != 11 || userCode[5] != '-'
            || userCode.Remove(5, 1).Any(character => !"0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character))
            || expiresAt <= DateTimeOffset.UnixEpoch || pollIntervalSeconds is < 5 or > 60)
        { throw new ArgumentException("The pairing response is invalid."); }
        UserCode = userCode;
        ExpiresAt = expiresAt;
        PollIntervalSeconds = pollIntervalSeconds;
    }
    public string DeviceCode { get; }
    public string UserCode { get; }
    public DateTimeOffset ExpiresAt { get; }
    public int PollIntervalSeconds { get; }
    public override string ToString() => "Pairing codes redacted";
}
