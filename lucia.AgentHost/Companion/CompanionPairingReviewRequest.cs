namespace lucia.AgentHost.Companion;

public sealed record CompanionPairingReviewRequest(string UserCode)
{
    public override string ToString() => "Companion pairing review code redacted";
}
