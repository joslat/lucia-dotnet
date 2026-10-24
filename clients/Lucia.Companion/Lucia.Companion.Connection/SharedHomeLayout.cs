using System.Globalization;
using Lucia.Companion.Core;

namespace Lucia.Companion.Connection;

/// <summary>The current permitted presentation metadata, never an owner document or an access grant.</summary>
public sealed class SharedHomeLayout
{
    public SharedHomeLayout(string homeId, string memberId, string revision, DateTimeOffset capturedAt, string name, bool hasUnqualifiedBindings,
        IReadOnlyList<SharedLayoutPlace> places, IReadOnlyList<SharedLayoutConnection> connections, IReadOnlyList<SharedLayoutBinding> bindings)
    {
        HomeId = WireValue.Identifier(homeId); MemberId = WireValue.Identifier(memberId); Name = Label(name);
        if (revision is null || revision.Length != 64 || revision.Any(character => !char.IsAsciiHexDigitUpper(character)) || capturedAt <= DateTimeOffset.UnixEpoch)
        { throw new ArgumentException("The shared layout revision or capture time is invalid."); }
        ArgumentNullException.ThrowIfNull(places); ArgumentNullException.ThrowIfNull(connections); ArgumentNullException.ThrowIfNull(bindings);
        if (places.Count is < 1 or > 200 || connections.Count > 1000 || bindings.Count > 2000
            || places.Any(place => place is null) || connections.Any(edge => edge is null) || bindings.Any(binding => binding is null))
        { throw new ArgumentException("Shared layout metadata exceeds its bounded capacity or contains null entries."); }
        var ids = new Dictionary<string, SharedLayoutPlace>(StringComparer.Ordinal);
        foreach (var place in places)
        {
            WireValue.Identifier(place.Id); WireValue.Identifier(place.SourcePlaceId); Label(place.Name);
            if (!Enum.IsDefined(place.Icon) || !ids.TryAdd(place.Id, place))
            { throw new ArgumentException("Shared layout places require unique IDs and a supported icon."); }
        }
        var edges = new HashSet<(string, string)>();
        foreach (var edge in connections)
        {
            if (edge.FromPlaceId is null || edge.ToPlaceId is null || edge.FromPlaceId == edge.ToPlaceId
                || !ids.ContainsKey(edge.FromPlaceId) || !ids.ContainsKey(edge.ToPlaceId)
                || !edges.Add(StringComparer.Ordinal.Compare(edge.FromPlaceId, edge.ToPlaceId) < 0
                    ? (edge.FromPlaceId, edge.ToPlaceId) : (edge.ToPlaceId, edge.FromPlaceId)))
            { throw new ArgumentException("Shared layout connections must join two configured places once."); }
        }
        var roles = new HashSet<(string, ObservationRole)>();
        foreach (var binding in bindings)
        {
            if (binding.PlaceId is null || !ids.TryGetValue(binding.PlaceId, out var place) || binding.SourcePlaceId != place.SourcePlaceId
                || !IsSourceId(binding.SourceId) || !Enum.IsDefined(binding.Field) || !roles.Add((binding.SourceId, binding.Field)))
            { throw new ArgumentException("Shared layout bindings require unique source roles and exact place provenance."); }
        }
        Revision = revision; CapturedAt = capturedAt; HasUnqualifiedBindings = hasUnqualifiedBindings;
        Places = Array.AsReadOnly(places.ToArray()); Connections = Array.AsReadOnly(connections.ToArray()); Bindings = Array.AsReadOnly(bindings.ToArray());
    }

    public string HomeId { get; }
    public string MemberId { get; }
    public string Revision { get; }
    public DateTimeOffset CapturedAt { get; }
    public string Name { get; }
    public bool HasUnqualifiedBindings { get; }
    public IReadOnlyList<SharedLayoutPlace> Places { get; }
    public IReadOnlyList<SharedLayoutConnection> Connections { get; }
    public IReadOnlyList<SharedLayoutBinding> Bindings { get; }

    internal void RequireGrant(DeviceSession session)
    {
        if (session.HomeId != HomeId || session.MemberId != MemberId
            || Places.Any(place => !session.Places.Any(allowed => allowed.PlaceId == place.SourcePlaceId && allowed.Fields.Count > 0))
            || Bindings.Any(binding => !session.Places.Any(allowed => allowed.PlaceId == binding.SourcePlaceId && allowed.Fields.Contains(binding.Field))))
        { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
    }

    private static bool IsSourceId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 256) { return false; }
        var parts = value.Split('.');
        return parts.Length == 2 && parts.All(part => part.Length > 0
            && part.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_'));
    }

    private static string Label(string value)
    {
        WireValue.Label(value);
        if (value != value.Trim()) { throw new ArgumentException("Shared layout labels must not have surrounding whitespace."); }
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsSurrogate(value[index])) { continue; }
            if (!char.IsHighSurrogate(value[index]) || index + 1 == value.Length || !char.IsLowSurrogate(value[++index]))
            { throw new ArgumentException("Shared layout labels require valid Unicode."); }
        }
        return value;
    }
}
