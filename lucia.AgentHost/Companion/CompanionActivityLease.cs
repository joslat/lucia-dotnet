namespace lucia.AgentHost.Companion;

/// <summary>A single attributed request. An unobserved outcome must not become a successful response.</summary>
public sealed class CompanionActivityLease : IDisposable
{
    private readonly Action<CompanionActivityState> update;
    private int finished;
    public string RequestId { get; }
    internal CompanionActivityLease(string requestId, Action<CompanionActivityState> update)
    { RequestId = requestId; this.update = update; }
    public void Working() { if (Volatile.Read(ref finished) == 0) { update(CompanionActivityState.Working); } }
    public void Complete(CompanionActivityState state)
    {
        if (!Enum.IsDefined(state) || state is CompanionActivityState.Received or CompanionActivityState.Working or CompanionActivityState.Continued)
        { throw new ArgumentException("A request completion requires its own reported final state, not inferred follow-up evidence.", nameof(state)); }
        if (Interlocked.Exchange(ref finished, 1) == 0) { update(state); }
    }
    public void Dispose() => Complete(CompanionActivityState.OutcomeUnknown);
}
