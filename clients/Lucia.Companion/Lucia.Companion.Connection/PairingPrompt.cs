namespace Lucia.Companion.Connection;

public sealed class PairingPrompt(string userCode, DateTimeOffset expiresAt, int pollIntervalSeconds)
{
    public string UserCode { get; } = userCode;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public int PollIntervalSeconds { get; } = pollIntervalSeconds;
    public override string ToString() => "Pairing display code redacted";
}
