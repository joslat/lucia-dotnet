namespace lucia.AgentHost.Companion;

public sealed record CompanionPairingRedeemRequest(string DeviceCode)
{
    public override string ToString() => "Companion device credential redacted";
}
