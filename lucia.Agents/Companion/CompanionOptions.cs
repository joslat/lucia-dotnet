namespace lucia.Agents.Companion;

public sealed class CompanionOptions
{
    public const string SectionName = "Companion";
    public bool Enabled { get; set; }
    public string HomeId { get; set; } = string.Empty;
    public TimeSpan AccessLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan RefreshLifetime { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(30);

    public void Validate()
    {
        if (Enabled) { CompanionIdentifier.Validate(HomeId); }
        if (AccessLifetime <= TimeSpan.Zero || AccessLifetime > TimeSpan.FromMinutes(15)
            || RefreshLifetime < AccessLifetime || RefreshLifetime > TimeSpan.FromDays(30)
            || SessionLifetime < RefreshLifetime || SessionLifetime > TimeSpan.FromDays(90))
        {
            throw new ArgumentException("Companion lifetimes must be positive: access at most 15 minutes, refresh at most 30 days, session at most 90 days, in ascending order.");
        }
    }
}
