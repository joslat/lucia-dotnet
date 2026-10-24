using lucia.HomeAssistant.Configuration;
using lucia.HomeAssistant.Models;
using lucia.HomeAssistant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace lucia.Agents.Companion;

/// <summary>Fresh HA metadata/report reads, filtered before any native or owner discovery serialization.</summary>
public sealed class CompanionHomeReader(IHomeAssistantClient source, IOptions<CompanionOptions> options,
    IOptionsMonitor<HomeAssistantOptions> sourceOptions, TimeProvider clock, ILogger<CompanionHomeReader> logger)
{
    public async Task<CompanionHomeSnapshot> ReadAsync(CompanionGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (grant.HomeId != options.Value.HomeId) { throw new ArgumentException("The read belongs to another home."); }
        if (!grant.Places.Any(place => place.Fields.Count > 0))
        { return new(grant.HomeId, clock.GetUtcNow(), options.Value.MaximumObservationAge.TotalSeconds, []); }
        return await ReadCoreAsync(grant, includeReadings: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Owner-only catalog. Calling code must enforce current owner authority, never a home/member policy.</summary>
    public Task<CompanionHomeSnapshot> ReadOwnerCatalogAsync(CancellationToken cancellationToken = default) =>
        ReadCoreAsync(null, includeReadings: false, cancellationToken);

    private async Task<CompanionHomeSnapshot> ReadCoreAsync(CompanionGrant? grant, bool includeReadings, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        return await CompanionSourceRead.RunAsync(settings, sourceOptions, clock, logger, async token =>
        {
            // No 24-hour location cache, phonetic/fuzzy lookup or fallback to old source metadata.
            var areaTask = source.GetAreaRegistryAsync(token);
            var entityTask = source.GetEntityRegistryAsync(token);
            var deviceTask = source.GetDeviceRegistryAsync(token);
            var stateTask = source.GetStatesAsync(token);
            await Task.WhenAll(areaTask, entityTask, deviceTask, stateTask).WaitAsync(token).ConfigureAwait(false);
            var areas = await areaTask.ConfigureAwait(false);
            var entities = await entityTask.ConfigureAwait(false);
            var devices = await deviceTask.ConfigureAwait(false);
            var states = await stateTask.ConfigureAwait(false);
            if (states.Length > 20_000)
            { throw new CompanionDataUnavailableException(tooLarge: true); }
            var locations = new CompanionRegistryLocations(areas, entities, devices);
            var areaMap = locations.Areas;
            var stateMap = CompanionRegistryLocations.Unique(states, state => state.EntityId, CompanionEntityIdentifier.IsValid);
            var located = new Dictionary<string, List<EntityRegistryEntry>>(StringComparer.Ordinal);
            foreach (var entity in locations.Entities.Values.OrderBy(entity => entity.EntityId, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                var areaId = locations.ResolveArea(entity);
                if (areaId is null) { continue; }
                if (!located.TryGetValue(areaId, out var inArea)) { inArea = []; located.Add(areaId, inArea); }
                inArea.Add(entity);
            }
            var now = clock.GetUtcNow();
            var projected = new List<CompanionPlaceSnapshot>();
            var totalSources = 0;
            foreach (var area in areaMap.Values.OrderBy(area => area.AreaId, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                var allowed = grant?.Places.SingleOrDefault(place => place.PlaceId == area.AreaId);
                if (grant is not null && (allowed is null || allowed.Fields.Count == 0)) { continue; }
                var sources = new List<CompanionSource>();
                var readings = new List<CompanionReading>();
                foreach (var entity in located.GetValueOrDefault(area.AreaId) ?? [])
                {
                    token.ThrowIfCancellationRequested();
                    if (!stateMap.TryGetValue(entity.EntityId, out var state)) { continue; }
                    var field = CompanionReadingMapper.Field(state);
                    if (field is null || allowed is not null && !allowed.Fields.Contains(field.Value)) { continue; }
                    sources.Add(new(entity.EntityId, CompanionRegistryLocations.Label(entity.Name ?? entity.OriginalName ?? CompanionReadingMapper.Attribute(state, "friendly_name"), entity.EntityId), field.Value));
                    if (includeReadings) { readings.Add(CompanionReadingMapper.Read(state, field.Value, now, settings.MaximumObservationAge)); }
                    if (++totalSources > 2000) { throw new CompanionDataUnavailableException(tooLarge: true); }
                }
                projected.Add(new(area.AreaId, CompanionRegistryLocations.Label(area.Name, area.AreaId), sources.AsReadOnly(), readings.AsReadOnly()));
            }
            return new CompanionHomeSnapshot(settings.HomeId, now, settings.MaximumObservationAge.TotalSeconds, projected.AsReadOnly());
        }, cancellationToken).ConfigureAwait(false);
    }

    public static CompanionHomeSnapshot Restrict(CompanionHomeSnapshot snapshot, CompanionGrant current) => snapshot with
    {
        Places = Array.AsReadOnly(snapshot.Places.Where(place => current.Places.Any(allowed => allowed.PlaceId == place.PlaceId && allowed.Fields.Count > 0))
            .Select(place => place with
            {
                Sources = Array.AsReadOnly(place.Sources.Where(source => current.Allows(snapshot.HomeId, place.PlaceId, source.Field)).ToArray()),
                Readings = Array.AsReadOnly(place.Readings.Where(reading => current.Allows(snapshot.HomeId, place.PlaceId, reading.Field)).ToArray())
            }).ToArray())
    };
}
