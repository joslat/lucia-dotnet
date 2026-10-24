namespace lucia.AgentHost.Companion;

/// <summary>A single attributed request. An unobserved outcome must not become a successful response.</summary>
public sealed class CompanionActivityLease : IDisposable
{
    private readonly Action<CompanionActivityState> update;
    private readonly Action<CompanionActivityCategory> describe;
    private readonly Action<IReadOnlyList<string>> targets;
    private int finished;
    public string RequestId { get; }
    internal CompanionActivityLease(string requestId, Action<CompanionActivityState> update, Action<CompanionActivityCategory> describe,
        Action<IReadOnlyList<string>> targets)
    { RequestId = requestId; this.update = update; this.describe = describe; this.targets = targets; }
    public void DescribeMatchedCommand(string skillId, string action)
    {
        if (Volatile.Read(ref finished) != 0) { return; }
        var category = (skillId, action) switch
        {
            ("LightControlSkill", "toggle" or "brightness") => CompanionActivityCategory.Lighting,
            ("ClimateControlSkill", "set_temperature" or "adjust") => CompanionActivityCategory.Climate,
            ("SceneControlSkill", "activate") => CompanionActivityCategory.Scene,
            _ => CompanionActivityCategory.General
        };
        describe(category);
    }
    public void Working() { if (Volatile.Read(ref finished) == 0) { update(CompanionActivityState.Working); } }
    public void ReportRequestedTargets(IReadOnlyList<string> entityIds) { if (Volatile.Read(ref finished) == 0) { targets(entityIds); } }
    public void Complete(CompanionActivityState state)
    {
        if (!Enum.IsDefined(state) || state is CompanionActivityState.Received or CompanionActivityState.Working or CompanionActivityState.Continued)
        { throw new ArgumentException("A request completion requires its own reported final state, not inferred follow-up evidence.", nameof(state)); }
        if (Interlocked.Exchange(ref finished, 1) == 0) { update(state); }
    }
    public void Dispose() => Complete(CompanionActivityState.OutcomeUnknown);
}
