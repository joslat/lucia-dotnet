namespace lucia.Agents.Companion;

public sealed class CompanionOptions
{
    public const string SectionName = "Companion";
    public bool Enabled { get; set; }
    public string HomeId { get; set; } = string.Empty;
    public TimeSpan AccessLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan RefreshLifetime { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan PairingLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public int MaximumPendingPairings { get; set; } = 32;
    public string SourceBaseUrl { get; set; } = string.Empty;
    public TimeSpan MaximumObservationAge { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan HomeReadTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public void Validate()
    {
        if (Enabled) { CompanionIdentifier.Validate(HomeId); }
        if (MaximumObservationAge < TimeSpan.FromSeconds(1) || MaximumObservationAge > TimeSpan.FromDays(1)
            || HomeReadTimeout < TimeSpan.FromSeconds(1) || HomeReadTimeout > TimeSpan.FromSeconds(30))
        { throw new ArgumentException("Home reads require a 1 to 30 second timeout and a 1 second to 1 day maximum observation age."); }
        if (SourceBaseUrl.Length > 0) { ValidateSourceUrl(SourceBaseUrl); }
        if (PairingLifetime < TimeSpan.FromMinutes(1) || PairingLifetime > TimeSpan.FromMinutes(15) || MaximumPendingPairings is < 1 or > 128)
        {
            throw new ArgumentException("Pairing lasts 1 to 15 minutes and permits 1 to 128 pending devices.");
        }
        if (AccessLifetime <= TimeSpan.Zero || AccessLifetime > TimeSpan.FromMinutes(15)
            || RefreshLifetime < AccessLifetime || RefreshLifetime > TimeSpan.FromDays(30)
            || SessionLifetime < RefreshLifetime || SessionLifetime > TimeSpan.FromDays(90))
        {
            throw new ArgumentException("Companion lifetimes must be positive: access at most 15 minutes, refresh at most 30 days, session at most 90 days, in ascending order.");
        }
    }

    public static Uri ValidateSourceUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
        { throw new ArgumentException("The companion source must be one HTTP(S) origin without credentials, path, query or fragment."); }
        return uri;
    }
}
