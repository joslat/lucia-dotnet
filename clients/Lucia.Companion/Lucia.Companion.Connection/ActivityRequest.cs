namespace Lucia.Companion.Connection;

public sealed class ActivityRequest
{
    public ActivityRequest(string requestId, RequestActivityState state, DateTimeOffset startedAt, DateTimeOffset updatedAt, long revision)
    {
        if (requestId is not { Length: 32 } || requestId.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            || !Enum.IsDefined(state) || startedAt <= DateTimeOffset.UnixEpoch || updatedAt < startedAt || revision < 1)
        { throw new ArgumentException("The request lifecycle record is invalid."); }
        RequestId = requestId; State = state; StartedAt = startedAt; UpdatedAt = updatedAt; Revision = revision;
    }
    public string RequestId { get; }
    public RequestActivityState State { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Revision { get; }
}
