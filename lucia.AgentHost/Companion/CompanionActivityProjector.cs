using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

/// <summary>Qualifies private exact target selectors into current authorized area metadata only.</summary>
public sealed class CompanionActivityProjector(CompanionTargetAreaReader reader)
{
    public async Task<CompanionActivitySnapshot> ReadAsync(CompanionActivityJournal journal, CompanionGrant grant, CancellationToken cancellationToken)
    {
        var (snapshot, targets) = journal.ReadForProjection(grant);
        if (targets.Count == 0 || !grant.Places.Any(place => place.Fields.Count > 0)) { return snapshot; }
        CompanionTargetAreaSnapshot areas;
        try { areas = await reader.ReadAsync(targets.Values.SelectMany(ids => ids).Distinct(StringComparer.Ordinal).ToArray(), grant, cancellationToken).ConfigureAwait(false); }
        catch (CompanionDataUnavailableException) { return snapshot; } // Lifecycle remains valid; no old or guessed areas substitute.
        var capturedAt = journal.CaptureTime;
        return snapshot with
        {
            CapturedAt = capturedAt,
            Requests = Array.AsReadOnly(snapshot.Requests.Select(item =>
            {
                if (!targets.TryGetValue(item.RequestId, out var ids)) { return item; }
                var known = ids.Where(areas.AreasByEntity.ContainsKey).ToArray();
                var places = known.Select(id => areas.AreasByEntity[id]).DistinctBy(place => place.PlaceId)
                    .OrderBy(place => place.PlaceId, StringComparer.Ordinal).ToArray();
                return places.Length == 0 ? item : item with
                {
                    CurrentTargetAreas = new(capturedAt, known.Length != ids.Count || places.Length > 6, Array.AsReadOnly(places.Take(6).ToArray()))
                };
            }).ToArray())
        };
    }

    public static CompanionActivitySnapshot Restrict(CompanionActivitySnapshot snapshot, CompanionGrant grant)
    {
        if (!grant.CanReadActivity || grant.HomeId != snapshot.HomeId || grant.MemberId != snapshot.MemberId) { throw new UnauthorizedAccessException(); }
        return snapshot with { Requests = Array.AsReadOnly(snapshot.Requests.Select(item =>
        {
            if (item.CurrentTargetAreas is not { } areas) { return item; }
            var places = areas.Places.Where(place => grant.Places.Any(allowed => allowed.PlaceId == place.PlaceId && allowed.Fields.Count > 0)).ToArray();
            return item with { CurrentTargetAreas = places.Length == 0 ? null : areas with
                { Places = Array.AsReadOnly(places), IsPartial = areas.IsPartial || places.Length != areas.Places.Count } };
        }).ToArray()) };
    }

    internal static CompanionActivitySnapshot LimitAreas(CompanionActivitySnapshot snapshot, int limit) => snapshot with
    {
        Requests = Array.AsReadOnly(snapshot.Requests.Select(item => item.CurrentTargetAreas is { } areas && areas.Places.Count > limit
            ? item with { CurrentTargetAreas = areas with { Places = Array.AsReadOnly(areas.Places.Take(limit).ToArray()), IsPartial = true } } : item).ToArray())
    };
}
