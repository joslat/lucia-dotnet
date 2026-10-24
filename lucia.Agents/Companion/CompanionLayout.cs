using System.Globalization;

namespace lucia.Agents.Companion;

/// <summary>Shared presentation metadata. Neither readings nor authority belong in this document.</summary>
public sealed class CompanionLayout
{
    public CompanionLayout(string homeId, string sourceBaseUrl, string name, IReadOnlyList<CompanionLayoutPlace> places,
        IReadOnlyList<CompanionLayoutConnection> connections, IReadOnlyList<CompanionLayoutBinding> bindings)
    {
        HomeId = CompanionIdentifier.Validate(homeId);
        SourceBaseUrl = CompanionOptions.ValidateSourceUrl(sourceBaseUrl).AbsoluteUri;
        Name = Label(name);
        ArgumentNullException.ThrowIfNull(places); ArgumentNullException.ThrowIfNull(connections); ArgumentNullException.ThrowIfNull(bindings);
        if (places.Count > 200 || connections.Count > 1000 || bindings.Count > 2000
            || places.Any(place => place is null) || connections.Any(edge => edge is null) || bindings.Any(binding => binding is null))
        { throw new ArgumentException("Layout metadata exceeds its bounded capacity or contains null entries."); }
        var ids = new Dictionary<string, CompanionLayoutPlace>(StringComparer.Ordinal);
        foreach (var place in places)
        {
            CompanionIdentifier.Validate(place.Id); CompanionIdentifier.Validate(place.SourcePlaceId); Label(place.Name);
            if (place.Icon is not ("Home" or "Sofa" or "Garden" or "Kitchen" or "Door" or "Bed") || !ids.TryAdd(place.Id, place))
            { throw new ArgumentException("Layout places require unique IDs and a supported outline icon."); }
        }
        var edges = new HashSet<(string, string)>();
        foreach (var edge in connections)
        {
            if (edge.FromPlaceId is null || edge.ToPlaceId is null || !ids.ContainsKey(edge.FromPlaceId) || !ids.ContainsKey(edge.ToPlaceId)
                || edge.FromPlaceId == edge.ToPlaceId || !edges.Add(StringComparer.Ordinal.Compare(edge.FromPlaceId, edge.ToPlaceId) < 0
                    ? (edge.FromPlaceId, edge.ToPlaceId) : (edge.ToPlaceId, edge.FromPlaceId)))
            { throw new ArgumentException("Connections must join two configured places once, in either direction."); }
        }
        var roles = new HashSet<(string, CompanionField)>();
        foreach (var binding in bindings)
        {
            if (binding.PlaceId is null || !ids.TryGetValue(binding.PlaceId, out var place) || binding.SourcePlaceId != place.SourcePlaceId
                || !CompanionEntityIdentifier.IsValid(binding.SourceId) || !Enum.IsDefined(binding.Field) || !roles.Add((binding.SourceId, binding.Field)))
            { throw new ArgumentException("Bindings require unique exact source roles and matching configured source-place provenance."); }
        }
        Places = Array.AsReadOnly(places.ToArray()); Connections = Array.AsReadOnly(connections.ToArray()); Bindings = Array.AsReadOnly(bindings.ToArray());
    }
    public string HomeId { get; }
    public string SourceBaseUrl { get; }
    public string Name { get; }
    public IReadOnlyList<CompanionLayoutPlace> Places { get; }
    public IReadOnlyList<CompanionLayoutConnection> Connections { get; }
    public IReadOnlyList<CompanionLayoutBinding> Bindings { get; }

    private static string Label(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value != value.Trim()
            || value.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        { throw new ArgumentException("Layout names require 1 to 200 visible characters without surrounding whitespace."); }
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsSurrogate(value[index])) { continue; }
            if (!char.IsHighSurrogate(value[index]) || index + 1 == value.Length || !char.IsLowSurrogate(value[++index]))
            { throw new ArgumentException("Layout names require valid Unicode."); }
        }
        return value;
    }
}
