using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

public sealed record CompanionPairingApproveRequest(bool CodeConfirmed, CompanionGrant Grant);
