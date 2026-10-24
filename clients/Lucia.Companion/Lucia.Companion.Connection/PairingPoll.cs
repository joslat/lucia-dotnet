namespace Lucia.Companion.Connection;

public sealed class PairingPoll(PairingPollStatus status, TimeSpan retryAfter, DeviceCredentials? credentials = null)
{
    public PairingPollStatus Status { get; } = status;
    public TimeSpan RetryAfter { get; } = retryAfter;
    public DeviceCredentials? Credentials { get; } = credentials;
    public override string ToString() => $"Pairing {Status}, credentials redacted";
}
