namespace Lucia.Companion.Connection;

public sealed class ActivitySnapshot
{
    public ActivitySnapshot(string homeId, string memberId, DateTimeOffset capturedAt, DateTimeOffset coverageStartedAt,
        bool isConfigured, bool hasGap, bool hasMore, IReadOnlyList<ActivityRequest> requests)
    {
        HomeId = WireValue.Identifier(homeId); MemberId = WireValue.Identifier(memberId);
        ArgumentNullException.ThrowIfNull(requests);
        if (coverageStartedAt <= DateTimeOffset.UnixEpoch || capturedAt < coverageStartedAt || requests.Count > 20
            || requests.Any(request => request is null || request.UpdatedAt > capturedAt
                || request.UpdatedAt < coverageStartedAt && request.State is not (RequestActivityState.Received or RequestActivityState.Working))
            || requests.Select(request => request.RequestId).Distinct(StringComparer.Ordinal).Count() != requests.Count
            || !isConfigured && (requests.Count > 0 || hasGap || hasMore) || hasMore && requests.Count != 20)
        { throw new ArgumentException("The attributed activity snapshot is invalid."); }
        CapturedAt = capturedAt; CoverageStartedAt = coverageStartedAt; IsConfigured = isConfigured; HasGap = hasGap; HasMore = hasMore;
        Requests = Array.AsReadOnly(requests.ToArray());
    }
    public string HomeId { get; }
    public string MemberId { get; }
    public DateTimeOffset CapturedAt { get; }
    public DateTimeOffset CoverageStartedAt { get; }
    public bool IsConfigured { get; }
    public bool HasGap { get; }
    public bool HasMore { get; }
    public IReadOnlyList<ActivityRequest> Requests { get; }
    internal void RequireGrant(DeviceSession session)
    {
        if (!session.CanReadActivity || session.HomeId != HomeId || session.MemberId != MemberId)
        { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
    }
}
