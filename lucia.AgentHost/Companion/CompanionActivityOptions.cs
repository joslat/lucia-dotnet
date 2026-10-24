namespace lucia.AgentHost.Companion;

/// <summary>Owner opt-in attribution. Keys are server-verified profile/key IDs, never request-body names or HA user IDs.</summary>
public sealed class CompanionActivityOptions
{
    public const string SectionName = "Companion:Activity";
    public bool Enabled { get; set; }
    public Dictionary<string, string> VoiceMembers { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> OwnerMembers { get; set; } = new(StringComparer.Ordinal);
    public void Validate()
    {
        if (VoiceMembers is null || OwnerMembers is null || VoiceMembers.Count + OwnerMembers.Count > 200)
        { throw new ArgumentException("Activity attribution supports at most 200 explicit source mappings."); }
        foreach (var mapping in VoiceMembers.Concat(OwnerMembers))
        {
            lucia.Agents.Companion.CompanionIdentifier.Validate(mapping.Key);
            lucia.Agents.Companion.CompanionIdentifier.Validate(mapping.Value);
        }
    }
}
