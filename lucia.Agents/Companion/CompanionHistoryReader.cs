using lucia.HomeAssistant.Configuration;
using lucia.HomeAssistant.Models;
using lucia.HomeAssistant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace lucia.Agents.Companion;

/// <summary>Bounded authorized recorder samples, without guessing historical room assignments or recorder coverage.</summary>
public sealed class CompanionHistoryReader(IHomeAssistantClient source, CompanionHomeReader catalog,
    IOptions<CompanionOptions> options, IOptionsMonitor<HomeAssistantOptions> sourceOptions,
    TimeProvider clock, ILogger<CompanionHistoryReader> logger)
{
    private const int MaximumSources = 256;
    private const int MaximumSamples = 50_000;

    public async Task<CompanionHistorySnapshot> ReadAsync(CompanionGrant grant, DateTimeOffset start, DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        cancellationToken.ThrowIfCancellationRequested();
        if (!grant.CanReadHistory) { throw new UnauthorizedAccessException("History access is not approved."); }
        var settings = options.Value;
        var now = clock.GetUtcNow();
        if (grant.HomeId != settings.HomeId || end <= start || end > now || end - start > TimeSpan.FromDays(8)
            || start < now.AddDays(-settings.HistoryLookbackDays))
        { throw new ArgumentException("History needs this home and a past, positive window of at most eight elapsed days within configured lookback."); }
        start = start.ToUniversalTime();
        end = end.ToUniversalTime();
        return await CompanionSourceRead.RunAsync(settings, sourceOptions, clock, logger, async token =>
        {
            var permitted = await catalog.ReadCatalogAsync(grant, token).ConfigureAwait(false);
            var metadata = permitted.Places.SelectMany(place => place.Sources.Select(item => (PlaceId: place.PlaceId, Source: item)))
                .OrderBy(item => item.Source.SourceId, StringComparer.Ordinal).ToArray();
            if (metadata.Length > MaximumSources || metadata.Select(item => item.Source.SourceId).Distinct(StringComparer.Ordinal).Count() != metadata.Length)
            { throw new CompanionDataUnavailableException(tooLarge: true); }
            var config = await source.GetConfigAsync(token).ConfigureAwait(false);
            if (!config.Components.Contains("history") || !config.Components.Contains("recorder") || string.IsNullOrWhiteSpace(config.TimeZone))
            { throw new CompanionDataUnavailableException(); }
            // Validate the source's calendar, but never return its coordinates, paths or location labels.
            var zone = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZone);
            var tracks = new List<CompanionHistoryTrack>();
            var sampleCount = 0;
            foreach (var batch in metadata.Chunk(16))
            {
                token.ThrowIfCancellationRequested();
                var requested = batch.ToDictionary(item => item.Source.SourceId, StringComparer.Ordinal);
                var history = await source.GetHistoryAsync(start.ToString("O"), string.Join(',', requested.Keys), end.ToString("O"),
                    minimalResponse: false, noAttributes: false, significantChangesOnly: false, cancellationToken: token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var received = new Dictionary<string, IReadOnlyList<CompanionHistorySample>>(StringComparer.Ordinal);
                if (history is null || history.Length > batch.Length) { throw new CompanionDataUnavailableException(); }
                foreach (var states in history)
                {
                    if (states is null) { throw new CompanionDataUnavailableException(); }
                    if (states.Length == 0) { continue; }
                    if (states[0] is null || !requested.TryGetValue(states[0].EntityId, out var item)
                        || received.ContainsKey(item.Source.SourceId)) { throw new CompanionDataUnavailableException(); }
                    var samples = new List<CompanionHistorySample>();
                    DateTimeOffset? previous = null;
                    foreach (var state in states)
                    {
                        token.ThrowIfCancellationRequested();
                        if (++sampleCount > MaximumSamples) { throw new CompanionDataUnavailableException(tooLarge: true); }
                        if (state is null || state.EntityId != item.Source.SourceId || state.LastUpdated == default
                            || state.LastUpdated.Kind == DateTimeKind.Unspecified) { throw new CompanionDataUnavailableException(); }
                        // Full recorder responses use last_updated, not the live last_reported heartbeat.
                        var at = new DateTimeOffset(state.LastUpdated.ToUniversalTime());
                        if (at < start || at > end || previous is not null && at <= previous)
                        { throw new CompanionDataUnavailableException(); }
                        previous = at;
                        if (at == end) { continue; }
                        var values = CompanionReadingMapper.Field(state) == item.Source.Field
                            ? CompanionReadingMapper.Values(state, item.Source.Field) : default;
                        var quality = state.State == "unavailable" ? CompanionHistoryQuality.Unavailable
                            : values.Number.HasValue || values.Active.HasValue ? CompanionHistoryQuality.Recorded : CompanionHistoryQuality.Unknown;
                        samples.Add(new(at, at == start, quality, values.Number, values.Active, values.Unit));
                    }
                    received.Add(item.Source.SourceId, samples.AsReadOnly());
                }
                foreach (var item in batch)
                { tracks.Add(new(item.Source.SourceId, item.PlaceId, item.Source.Field, received.GetValueOrDefault(item.Source.SourceId) ?? Array.Empty<CompanionHistorySample>())); }
            }
            return new CompanionHistorySnapshot(settings.HomeId, clock.GetUtcNow(), start, end, zone.Id, tracks.AsReadOnly());
        }, cancellationToken).ConfigureAwait(false);
    }

    public static CompanionHistorySnapshot Restrict(CompanionHistorySnapshot snapshot, CompanionGrant current) => snapshot with
    {
        Tracks = Array.AsReadOnly(snapshot.Tracks.Where(track => current.CanReadHistory
            && current.Allows(snapshot.HomeId, track.CurrentPlaceId, track.Field)).ToArray())
    };
}
