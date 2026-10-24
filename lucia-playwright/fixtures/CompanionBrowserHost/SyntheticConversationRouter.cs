using lucia.Wyoming.CommandRouting;

// Dedicated loopback qualification only. Holds a real processor request, never calls a model or physical skill.
internal sealed class SyntheticConversationRouter : ICommandRouter
{
    private readonly object gate = new();
    private TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int waiting;
    public bool FallbackToLlmEnabled => false;
    public bool Waiting => Volatile.Read(ref waiting) != 0;
    public void Release() { lock (gate) { release.TrySetResult(); } }
    public bool Reset()
    {
        lock (gate)
        {
            if (waiting != 0) { return false; }
            release = new(TaskCreationOptions.RunContinuationsAsynchronously); return true;
        }
    }
    public async Task<CommandRouteResult> RouteAsync(string transcript, CancellationToken ct)
    {
        Task pending;
        lock (gate) { waiting++; pending = release.Task; }
        try { await pending.WaitAsync(ct); }
        finally { lock (gate) { waiting--; } }
        return new() { IsMatch = true, Confidence = 1,
            MatchedPattern = new() { Id = "synthetic", SkillId = "synthetic", Action = "read", Templates = ["PRIVATE_"] } };
    }
}
