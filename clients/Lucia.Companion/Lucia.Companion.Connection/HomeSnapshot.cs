using Lucia.Companion.Core;

namespace Lucia.Companion.Connection;

public sealed class HomeSnapshot
{
    public HomeSnapshot(string homeId, DateTimeOffset capturedAt, double maximumObservationAgeSeconds, IReadOnlyList<HomePlaceSnapshot> places)
    {
        Places = HomeContract.Places(homeId, capturedAt, places, place => place.PlaceId, place => place.Sources);
        if (!double.IsFinite(maximumObservationAgeSeconds) || maximumObservationAgeSeconds is < 1 or > 86400)
        { throw new ArgumentException("The report age budget is invalid."); }
        foreach (var report in Places.SelectMany(place => place.Readings))
        {
            var age = capturedAt - report.ReportedAt;
            if (report.ReportedAt > capturedAt
                || report.Quality == ObservationQuality.Current && age?.TotalSeconds > maximumObservationAgeSeconds
                || report.Quality == ObservationQuality.Stale && age?.TotalSeconds <= maximumObservationAgeSeconds)
            { throw new ArgumentException("The source report time and quality disagree."); }
        }
        HomeId = homeId; CapturedAt = capturedAt; MaximumObservationAgeSeconds = maximumObservationAgeSeconds;
    }
    public string HomeId { get; }
    public DateTimeOffset CapturedAt { get; }
    public double MaximumObservationAgeSeconds { get; }
    public IReadOnlyList<HomePlaceSnapshot> Places { get; }
    internal void RequireGrant(DeviceSession session) => HomeContract.RequireGrant(session, HomeId, Places.Select(place => (place.PlaceId, place.Sources)));
}
