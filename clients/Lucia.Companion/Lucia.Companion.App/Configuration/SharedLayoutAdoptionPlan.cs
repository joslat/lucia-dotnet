using Lucia.Companion.App.Persistence;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Configuration;

/// <summary>A reviewed metadata replacement. Callers must obtain the current layout through the protected connection before applying.</summary>
public sealed class SharedLayoutAdoptionPlan
{
    private readonly HomeTopology baseline;
    private readonly string profileScope;
    private readonly string sessionId;
    private readonly IReadOnlyList<SharedLayoutPlace> reviewedPlaces;

    private SharedLayoutAdoptionPlan(HomeTopology baseline, HomeTopology preview, SharedHomeLayout layout, ConnectionSession identity)
    {
        this.baseline = baseline;
        Preview = preview;
        Revision = layout.Revision;
        HasUnqualifiedBindings = layout.HasUnqualifiedBindings;
        profileScope = ConnectedProfileScope.FileName(identity);
        sessionId = identity.Session.SessionId;
        reviewedPlaces = layout.Places;
    }

    public HomeTopology Preview { get; }
    public string Revision { get; }
    public bool HasUnqualifiedBindings { get; }

    /// <summary>Replaces presentation, never permission or observations. Personal preferences are outside this plan.</summary>
    public static SharedLayoutAdoptionPlan Create(HomeTopology original, SharedHomeLayout layout, ConnectionSession identity)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(identity);
        if (original.Id != layout.HomeId || !Permitted(layout, identity.Session))
        { throw new ArgumentException("Review shared metadata belonging to this authorized home and member."); }
        var preview = Project(layout);
        HomeProfile.From(preview, []).Validate();
        return new(original, preview, layout, identity);
    }

    public HomeTopology ApplyTo(HomeTopology current, SharedHomeLayout currentLayout, ConnectionSession currentIdentity)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(currentLayout);
        ArgumentNullException.ThrowIfNull(currentIdentity);
        if (currentLayout.Revision != Revision || currentLayout.HasUnqualifiedBindings != HasUnqualifiedBindings
            || currentIdentity.Session.SessionId != sessionId || ConnectedProfileScope.FileName(currentIdentity) != profileScope
            || !Permitted(currentLayout, currentIdentity.Session) || !SameMetadata(current, baseline)
            || !currentLayout.Places.SequenceEqual(reviewedPlaces) || !SameMetadata(Project(currentLayout), Preview))
        { throw new InvalidOperationException("The local setup, shared layout or connection changed. Review the latest versions before adopting."); }
        return Preview;
    }

    private static HomeTopology Project(SharedHomeLayout layout) => new(layout.HomeId, layout.Name,
        layout.Places.Select(place => new Place(place.Id, place.Name, PlaceState.Unavailable,
            ObservationBinding.MissingReadingsReason(layout.Bindings.Any(binding => binding.PlaceId == place.Id)), []) { Icon = place.Icon }),
        layout.Connections.Select(edge => new PlaceConnection(edge.FromPlaceId, edge.ToPlaceId)),
        layout.Bindings.Select(binding => new ObservationBinding(binding.PlaceId, binding.SourceId, binding.Field, binding.SourcePlaceId)));

    private static bool Permitted(SharedHomeLayout layout, DeviceSession identity) => layout.HomeId == identity.HomeId && layout.MemberId == identity.MemberId
        && layout.Places.All(place => identity.Places.Any(allowed => allowed.PlaceId == place.SourcePlaceId && allowed.Fields.Count > 0))
        && layout.Bindings.All(binding => identity.Places.Any(allowed => allowed.PlaceId == binding.SourcePlaceId && allowed.Fields.Contains(binding.Field)));

    private static bool SameMetadata(HomeTopology left, HomeTopology right) => left.Id == right.Id && left.Name == right.Name
        && left.Places.Select(place => (place.Id, place.Name, place.Icon)).SequenceEqual(right.Places.Select(place => (place.Id, place.Name, place.Icon)))
        && left.Bindings.ToHashSet().SetEquals(right.Bindings)
        && left.Connections.Select(EdgeKey).ToHashSet().SetEquals(right.Connections.Select(EdgeKey));

    private static (string, string) EdgeKey(PlaceConnection edge) => StringComparer.Ordinal.Compare(edge.FromPlaceId, edge.ToPlaceId) < 0
        ? (edge.FromPlaceId, edge.ToPlaceId) : (edge.ToPlaceId, edge.FromPlaceId);
}
