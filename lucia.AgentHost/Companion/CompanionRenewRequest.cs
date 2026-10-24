namespace lucia.AgentHost.Companion;

public sealed record CompanionRenewRequest(string RefreshToken)
{
    public override string ToString() => "Companion refresh credential redacted";
}
