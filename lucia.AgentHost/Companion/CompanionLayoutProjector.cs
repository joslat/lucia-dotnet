using System.Security.Cryptography;
using System.Text.Json;
using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

public static class CompanionLayoutProjector
{
    public static bool HasEligiblePlaces(CompanionLayout layout, CompanionGrant grant) =>
        layout.HomeId == grant.HomeId && layout.Places.Any(place => grant.Places.Any(allowed => allowed.PlaceId == place.SourcePlaceId && allowed.Fields.Count > 0));

    public static bool MatchesCatalog(CompanionLayout layout, CompanionHomeSnapshot catalog) => layout.HomeId == catalog.HomeId
        && layout.Places.All(place => catalog.Places.Any(sourcePlace => sourcePlace.PlaceId == place.SourcePlaceId))
        && layout.Bindings.All(binding => catalog.Places.Any(place => place.PlaceId == binding.SourcePlaceId
            && place.Sources.Any(source => source.SourceId == binding.SourceId && source.Field == binding.Field)));

    public static CompanionMemberLayout? Project(CompanionLayout layout, CompanionHomeSnapshot catalog, CompanionSession session)
    {
        var grant = session.Grant;
        if (layout.HomeId != grant.HomeId || catalog.HomeId != grant.HomeId) { throw new UnauthorizedAccessException(); }
        var allowedCatalog = CompanionHomeReader.Restrict(catalog, grant);
        var places = Array.AsReadOnly(layout.Places.Where(place => allowedCatalog.Places.Any(sourcePlace => sourcePlace.PlaceId == place.SourcePlaceId)).ToArray());
        if (places.Count == 0) { return null; }
        var ids = places.Select(place => place.Id).ToHashSet(StringComparer.Ordinal);
        var edges = Array.AsReadOnly(layout.Connections.Where(edge => ids.Contains(edge.FromPlaceId) && ids.Contains(edge.ToPlaceId)).ToArray());
        var eligibleBindings = layout.Bindings.Where(binding => ids.Contains(binding.PlaceId) && grant.Allows(layout.HomeId, binding.SourcePlaceId, binding.Field)).ToArray();
        var bindings = Array.AsReadOnly(eligibleBindings.Where(binding => allowedCatalog.Places.Any(place => place.PlaceId == binding.SourcePlaceId
                && place.Sources.Any(source => source.SourceId == binding.SourceId && source.Field == binding.Field))).ToArray());
        var hasUnqualifiedBindings = eligibleBindings.Length != bindings.Count;
        // Do not hash the storage revision, audit time, source origin, hidden places or excluded bindings.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            session.Id, grant.HomeId, grant.MemberId, grant = grant.Places.OrderBy(place => place.PlaceId, StringComparer.Ordinal)
                .Select(place => new { place.PlaceId, fields = place.Fields.OrderBy(field => field) }), layout.Name, hasUnqualifiedBindings, places, connections = edges, bindings
        }, CompanionApiJson.Options);
        return new(layout.HomeId, grant.MemberId, Convert.ToHexString(SHA256.HashData(bytes)), catalog.CapturedAt, layout.Name, hasUnqualifiedBindings, places, edges, bindings);
    }
}
