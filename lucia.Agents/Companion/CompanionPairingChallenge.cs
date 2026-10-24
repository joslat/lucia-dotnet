namespace lucia.Agents.Companion;

/// <summary>DeviceCode is a secret. UserCode only identifies an intent for owner review, never redemption.</summary>
public sealed class CompanionPairingChallenge(string deviceCode, string userCode, DateTimeOffset expiresAt)
{
    public string DeviceCode { get; } = deviceCode;
    public string UserCode { get; } = userCode;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public int PollIntervalSeconds => 5;
    public override string ToString() => "Companion pairing credentials redacted";
}
