using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

/// <summary>Only this filtered shape reaches members. Source origin, owner audit and storage revision are absent.</summary>
public sealed record CompanionMemberLayout(string HomeId, string MemberId, string Revision, DateTimeOffset CapturedAt, string Name, bool HasUnqualifiedBindings,
    IReadOnlyList<CompanionLayoutPlace> Places, IReadOnlyList<CompanionLayoutConnection> Connections, IReadOnlyList<CompanionLayoutBinding> Bindings);
