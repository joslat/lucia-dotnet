using lucia.HomeAssistant.Configuration;
using lucia.HomeAssistant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace lucia.Agents.Companion;

public sealed class CompanionTargetAreaReader(IHomeAssistantClient source, IOptions<CompanionOptions> options,
    IOptionsMonitor<HomeAssistantOptions> sourceOptions, TimeProvider clock, ILogger<CompanionTargetAreaReader> logger)
{
    public Task<CompanionTargetAreaSnapshot> ReadAsync(IReadOnlyCollection<string> targets, CompanionGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets); ArgumentNullException.ThrowIfNull(grant);
        if (!grant.CanReadActivity || grant.HomeId != options.Value.HomeId) { throw new UnauthorizedAccessException("Target areas require current activity and home authority."); }
        if (targets.Count > 640) { throw new CompanionDataUnavailableException(tooLarge: true); }
        var ids = targets.Where(CompanionEntityIdentifier.IsValid).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0 || !grant.Places.Any(place => place.Fields.Count > 0))
        { return Task.FromResult(new CompanionTargetAreaSnapshot(clock.GetUtcNow(), new Dictionary<string, CompanionTargetArea>(StringComparer.Ordinal))); }
        return CompanionSourceRead.RunAsync(options.Value, sourceOptions, clock, logger, async token =>
        {
            var areas = source.GetAreaRegistryAsync(token);
            var entities = source.GetEntityRegistryAsync(token);
            var devices = source.GetDeviceRegistryAsync(token);
            await Task.WhenAll(areas, entities, devices).WaitAsync(token).ConfigureAwait(false);
            var locations = new CompanionRegistryLocations(await areas.ConfigureAwait(false), await entities.ConfigureAwait(false), await devices.ConfigureAwait(false));
            var result = new Dictionary<string, CompanionTargetArea>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                token.ThrowIfCancellationRequested();
                if (!locations.Entities.TryGetValue(id, out var entity) || locations.ResolveArea(entity) is not { } placeId
                    || !grant.Places.Any(place => place.PlaceId == placeId && place.Fields.Count > 0)) { continue; }
                result.Add(id, new(placeId, CompanionRegistryLocations.Label(locations.Areas[placeId].Name, placeId)));
            }
            return new CompanionTargetAreaSnapshot(clock.GetUtcNow(), new System.Collections.ObjectModel.ReadOnlyDictionary<string, CompanionTargetArea>(result));
        }, cancellationToken);
    }
}
