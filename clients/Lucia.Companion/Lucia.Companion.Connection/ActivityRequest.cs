namespace Lucia.Companion.Connection;

public sealed class ActivityRequest
{
    public ActivityRequest(string requestId, RequestActivityState state, DateTimeOffset startedAt, DateTimeOffset updatedAt, long revision, string? previousRequestId = null)
    {
        if (!IsRequestId(requestId) || previousRequestId is not null && (!IsRequestId(previousRequestId) || previousRequestId == requestId)
            || !Enum.IsDefined(state) || startedAt <= DateTimeOffset.UnixEpoch || updatedAt < startedAt || revision < 1)
        { throw new ArgumentException("The request lifecycle record is invalid."); }
        RequestId = requestId; State = state; StartedAt = startedAt; UpdatedAt = updatedAt; Revision = revision; PreviousRequestId = previousRequestId;
    }
    public string RequestId { get; }
    public RequestActivityState State { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Revision { get; }
    public string? PreviousRequestId { get; }
    private static bool IsRequestId(string value) => value is { Length: 32 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
