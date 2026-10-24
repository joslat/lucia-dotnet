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

    public void Validate()
    {
        if (Enabled) { CompanionIdentifier.Validate(HomeId); }
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
}
