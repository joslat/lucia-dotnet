namespace Lucia.Companion.Connection;

public sealed record PairingUpdate(PairingPollStatus Status, TimeSpan RetryAfter, ConnectionSession? Connection = null);
