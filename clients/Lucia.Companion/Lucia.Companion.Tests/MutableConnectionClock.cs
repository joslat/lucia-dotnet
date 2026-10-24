namespace Lucia.Companion.Tests;

internal sealed class MutableConnectionClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
    public long TimestampSeconds { get; set; }
    public override long TimestampFrequency => 1;
    public override long GetTimestamp() => TimestampSeconds;
}
