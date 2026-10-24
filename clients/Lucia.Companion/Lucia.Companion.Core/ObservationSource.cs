namespace Lucia.Companion.Core;

/// <summary>One source exposed by discovery. It grants no access and contains no observed value or credential.</summary>
public sealed record ObservationSource
{
    public ObservationSource(string id, string name, IEnumerable<ObservationRole> supportedRoles, string? sourcePlaceId = null)
    {
        ValidateId(id);
        ValidateSourcePlaceId(sourcePlaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(supportedRoles);
        var roles = supportedRoles.ToArray();
        if (name.Length > 200 || roles.Length == 0 || roles.Any(role => !Enum.IsDefined(role)) || roles.Distinct().Count() != roles.Length)
        {
            throw new ArgumentException("A source needs a bounded name and unique supported observation roles.");
        }
        Id = id;
        Name = name;
        SupportedRoles = Array.AsReadOnly(roles);
        SourcePlaceId = sourcePlaceId;
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<ObservationRole> SupportedRoles { get; }
    public string? SourcePlaceId { get; }

    internal static void ValidateSourcePlaceId(string? id)
    {
        if (id is not null && (id.Length is 0 or > 128 || id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.' and not ':')))
        { throw new ArgumentException("The source place identifier is invalid."); }
    }

    internal static void ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length > 256 || id.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException("A source identifier must be at most 256 characters without whitespace or controls.", nameof(id));
        }
    }
}
