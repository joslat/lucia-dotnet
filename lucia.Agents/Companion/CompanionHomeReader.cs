using System.Globalization;
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
        if (!MatchesSource(sourceOptions.CurrentValue.BaseUrl)) { throw new CompanionDataUnavailableException(); }
        var sourceChanged = 0;
        using var changes = sourceOptions.OnChange((current, _) =>
        { if (!MatchesSource(current.BaseUrl)) { Interlocked.Exchange(ref sourceChanged, 1); } });
        using var timeout = new CancellationTokenSource(settings.HomeReadTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            // No 24-hour location cache, phonetic/fuzzy lookup or fallback to old source metadata.
            var areaTask = source.GetAreaRegistryAsync(linked.Token);
            var entityTask = source.GetEntityRegistryAsync(linked.Token);
            var deviceTask = source.GetDeviceRegistryAsync(linked.Token);
            var stateTask = source.GetStatesAsync(linked.Token);
            await Task.WhenAll(areaTask, entityTask, deviceTask, stateTask).WaitAsync(linked.Token).ConfigureAwait(false);
            var areas = await areaTask.ConfigureAwait(false);
            var entities = await entityTask.ConfigureAwait(false);
            var devices = await deviceTask.ConfigureAwait(false);
            var states = await stateTask.ConfigureAwait(false);
            if (areas.Length > 2000 || entities.Length > 20_000 || devices.Length > 20_000 || states.Length > 20_000)
            { throw new CompanionDataUnavailableException(tooLarge: true); }
            var areaMap = Unique(areas, area => area.AreaId, ValidId);
            var entityMap = Unique(entities, entity => entity.EntityId, ValidEntityId);
            var deviceMap = Unique(devices, device => device.Id, ValidId);
            var stateMap = Unique(states, state => state.EntityId, ValidEntityId);
            var located = new Dictionary<string, List<EntityRegistryEntry>>(StringComparer.Ordinal);
            foreach (var entity in entityMap.Values.OrderBy(entity => entity.EntityId, StringComparer.Ordinal))
            {
                linked.Token.ThrowIfCancellationRequested();
                if (entity.DisabledBy is not null || entity.HiddenBy is not null) { continue; }
                var areaId = ResolveArea(entity, deviceMap);
                if (areaId is null || !areaMap.ContainsKey(areaId)) { continue; }
                if (!located.TryGetValue(areaId, out var inArea)) { inArea = []; located.Add(areaId, inArea); }
                inArea.Add(entity);
            }
            var now = clock.GetUtcNow();
            var projected = new List<CompanionPlaceSnapshot>();
            var totalSources = 0;
            foreach (var area in areaMap.Values.OrderBy(area => area.AreaId, StringComparer.Ordinal))
            {
                linked.Token.ThrowIfCancellationRequested();
                var allowed = grant?.Places.SingleOrDefault(place => place.PlaceId == area.AreaId);
                if (grant is not null && (allowed is null || allowed.Fields.Count == 0)) { continue; }
                var sources = new List<CompanionSource>();
                var readings = new List<CompanionReading>();
                foreach (var entity in located.GetValueOrDefault(area.AreaId) ?? [])
                {
                    linked.Token.ThrowIfCancellationRequested();
                    if (!stateMap.TryGetValue(entity.EntityId, out var state)) { continue; }
                    var field = CompanionReadingMapper.Field(state);
                    if (field is null || allowed is not null && !allowed.Fields.Contains(field.Value)) { continue; }
                    sources.Add(new(entity.EntityId, Label(entity.Name ?? entity.OriginalName ?? CompanionReadingMapper.Attribute(state, "friendly_name"), entity.EntityId), field.Value));
                    if (includeReadings) { readings.Add(CompanionReadingMapper.Read(state, field.Value, now, settings.MaximumObservationAge)); }
                    if (++totalSources > 2000) { throw new CompanionDataUnavailableException(tooLarge: true); }
                }
                projected.Add(new(area.AreaId, Label(area.Name, area.AreaId), sources.AsReadOnly(), readings.AsReadOnly()));
            }
            if (Volatile.Read(ref sourceChanged) != 0 || !MatchesSource(sourceOptions.CurrentValue.BaseUrl))
            { throw new CompanionDataUnavailableException(); }
            return new(settings.HomeId, now, settings.MaximumObservationAge.TotalSeconds, projected.AsReadOnly());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (CompanionDataUnavailableException) { throw; }
        catch (Exception error)
        {
            logger.LogWarning("Companion home source read failed with {FailureType}; no cached household fallback was returned", error.GetType().Name);
            throw new CompanionDataUnavailableException();
        }
    }

    private bool MatchesSource(string value)
    {
        try { return options.Value.SourceBaseUrl.Length > 0 && CompanionOptions.ValidateSourceUrl(value) == CompanionOptions.ValidateSourceUrl(options.Value.SourceBaseUrl); }
        catch (ArgumentException) { return false; }
    }

    private static string? ResolveArea(EntityRegistryEntry entity, IReadOnlyDictionary<string, DeviceRegistryEntry> devices)
    {
        var area = entity.AreaId;
        var deviceId = entity.DeviceId;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (deviceId is not null)
        {
            if (seen.Count >= 32 || !seen.Add(deviceId) || !devices.TryGetValue(deviceId, out var device) || device.DisabledBy is not null)
            { return null; }
            area ??= device.AreaId;
            deviceId = device.ParentDeviceId;
        }
        return area;
    }
    private static Dictionary<string, T> Unique<T>(IEnumerable<T> items, Func<T, string> id, Func<string, bool> valid) where T : class =>
        items.Where(item => item is not null && valid(id(item))).GroupBy(id, StringComparer.Ordinal)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
    private static bool ValidId(string value) => !string.IsNullOrEmpty(value) && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or ':');
    private static bool ValidEntityId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 256) { return false; }
        var parts = value.Split('.');
        return parts.Length == 2 && parts.All(part => part.Length > 0 && part.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_'));
    }
    private static string Label(string? value, string fallback)
    {
        var clean = new string((value ?? string.Empty).Where(character => !char.IsControl(character) && char.GetUnicodeCategory(character) != UnicodeCategory.Format).Take(200).ToArray()).Trim();
        return clean.Length > 0 ? clean : fallback[..Math.Min(200, fallback.Length)];
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
