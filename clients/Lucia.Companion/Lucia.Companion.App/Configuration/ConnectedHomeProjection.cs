using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using System.Globalization;

namespace Lucia.Companion.App.Configuration;

public static class ConnectedHomeProjection
{
    /// <summary>Map explicit configured bindings to authorized reports. Layout is never inferred from sensor activity.</summary>
    public static HomeTopology Project(HomeTopology setup, HomeSnapshot snapshot, TimeSpan elapsedSinceFetch)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (setup.Id != snapshot.HomeId) { throw new ArgumentException("The reports belong to another home."); }
        if (elapsedSinceFetch < TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(elapsedSinceFetch)); }
        var reports = snapshot.Places.SelectMany(place =>
        {
            var sources = place.Sources.ToDictionary(source => source.SourceId, StringComparer.Ordinal);
            return place.Readings.Select(reading => (place.PlaceId, Name: sources[reading.SourceId].Name, Reading: reading));
        }).ToDictionary(item => (item.Reading.SourceId, item.Reading.Field));
        var places = setup.Places.Select(place =>
        {
            var observations = new List<Observation>();
            var evidence = new List<(ObservationRole Role, bool? Active, Observation Observation)>();
            var needsReview = false;
            foreach (var binding in setup.Bindings.Where(binding => binding.PlaceId == place.Id))
            {
                if (binding.SourcePlaceId is null || !reports.TryGetValue((binding.SourceId, binding.Role), out var report) || report.PlaceId != binding.SourcePlaceId)
                {
                    needsReview = true;
                    observations.Add(new(binding.Role.ToString(), null, ObservationQuality.Unavailable, null, ObservationTimeBasis.Reported));
                    continue;
                }
                var quality = report.Reading.Quality;
                if (quality == ObservationQuality.Current && (snapshot.CapturedAt - report.Reading.ReportedAt!.Value).TotalSeconds + elapsedSinceFetch.TotalSeconds > snapshot.MaximumObservationAgeSeconds)
                { quality = ObservationQuality.Stale; }
                var observation = new Observation($"{report.Name} · {binding.Role}", Value(report.Reading), quality, report.Reading.ReportedAt, ObservationTimeBasis.Reported);
                observations.Add(observation);
                evidence.Add((binding.Role, report.Reading.IsActive, observation));
            }
            var status = Status(observations, evidence, needsReview);
            return place with { State = status.State, Reason = status.Reason, Observations = observations.AsReadOnly() };
        });
        return new(setup.Id, setup.Name, places, setup.Connections, setup.Bindings);
    }

    public static IReadOnlyList<ObservationSource> Sources(HomeSnapshot snapshot) => Array.AsReadOnly(snapshot.Places.SelectMany(place =>
        place.Sources.Select(source => new ObservationSource(source.SourceId, source.Name, [source.Field], place.PlaceId))).ToArray());

    public static HomeCatalog Catalog(HomeSnapshot snapshot) => new(snapshot.HomeId, snapshot.CapturedAt,
        snapshot.Places.Select(place => new HomePlaceCatalog(place.PlaceId, place.Name, place.Sources)).ToArray());

    private static string? Value(HomeReading report) => report.NumberValue is { } number ? number.ToString("G", CultureInfo.CurrentCulture) + " " + report.Unit
        : report.IsActive is { } active ? report.Field switch
        {
            ObservationRole.Contact => active ? "Open" : "Closed",
            ObservationRole.Leak => active ? "Wet" : "Dry",
            ObservationRole.Light => active ? "On" : "Off",
            ObservationRole.Motion => active ? "Active" : "Inactive",
            _ => null
        } : null;

    private static (PlaceState State, string Reason) Status(IReadOnlyList<Observation> observations,
        IReadOnlyList<(ObservationRole Role, bool? Active, Observation Observation)> evidence, bool needsReview)
    {
        var current = evidence.Where(item => item.Observation.Quality == ObservationQuality.Current && item.Observation.Value is not null).ToArray();
        if (current.Any(item => item.Role == ObservationRole.Leak && item.Active == true)) { return (PlaceState.Problem, "Moisture sensor reports wet."); }
        if (current.Any(item => item.Role == ObservationRole.Contact && item.Active == true)) { return (PlaceState.Changed, "Contact sensor reports open."); }
        if (current.Any(item => item.Role == ObservationRole.Motion && item.Active == true)) { return (PlaceState.Changed, "Motion sensor reports activity, not presence."); }
        if (needsReview) { return (PlaceState.Unavailable, "Sources are missing, moved or unqualified. Review the bindings; no current report is inferred."); }
        if (observations.Count == 0) { return (PlaceState.Unavailable, ObservationBinding.MissingReadingsReason(false)); }
        if (current.Length != observations.Count) { return (PlaceState.Unavailable, "Some configured reports are stale, unknown or unavailable."); }
        return (PlaceState.Quiet, "Recent source reports.");
    }
}
