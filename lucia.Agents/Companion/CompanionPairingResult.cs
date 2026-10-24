namespace lucia.Agents.Companion;

public sealed class CompanionPairingResult(CompanionPairingStatus status, CompanionSessionTokens? tokens = null)
{
    public CompanionPairingStatus Status { get; } = status;
    public CompanionSessionTokens? Tokens { get; } = tokens;
    public override string ToString() => $"Companion pairing result {Status}, credentials redacted";
}
