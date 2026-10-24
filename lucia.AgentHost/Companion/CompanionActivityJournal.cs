using lucia.Agents.Companion;
using Microsoft.Extensions.Options;

namespace lucia.AgentHost.Companion;

/// <summary>Bounded, volatile, per-member request lifecycle. Never reads the global task/trace stores.</summary>
public sealed class CompanionActivityJournal
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);
    private readonly object gate = new();
    private readonly string homeId;
    private readonly bool enabled;
    private readonly Dictionary<string, string> voiceMembers;
    private readonly Dictionary<string, string> ownerMembers;
    private readonly HashSet<string> members;
    private readonly Dictionary<string, List<CompanionActivityItem>> requests = new(StringComparer.Ordinal);
    private readonly HashSet<string> gaps = new(StringComparer.Ordinal);
    private readonly TimeProvider clock;
    private readonly DateTimeOffset startedAt;
    private readonly long startedTimestamp;

    public CompanionActivityJournal(IOptions<CompanionOptions> home, IOptions<CompanionActivityOptions> activity, TimeProvider clock)
    {
        home.Value.Validate(); activity.Value.Validate();
        homeId = home.Value.HomeId; enabled = home.Value.Enabled && activity.Value.Enabled;
        voiceMembers = new(activity.Value.VoiceMembers, StringComparer.Ordinal);
        ownerMembers = new(activity.Value.OwnerMembers, StringComparer.Ordinal);
        members = voiceMembers.Values.Concat(ownerMembers.Values).ToHashSet(StringComparer.Ordinal);
        this.clock = clock; startedAt = clock.GetUtcNow(); startedTimestamp = clock.GetTimestamp();
    }

    // Call sites must supply a freshly verified source identity. No body field is an attribution credential.
    public CompanionActivityLease? BeginVoice(string? verifiedProfileId) => Begin(voiceMembers, verifiedProfileId);
    public CompanionActivityLease? BeginOwner(string? currentOwnerKeyId) => Begin(ownerMembers, currentOwnerKeyId);
    internal bool HasOwnerMapping(string? ownerKeyId) => enabled && ownerKeyId is not null && ownerMembers.ContainsKey(ownerKeyId);

    public CompanionActivitySnapshot Read(CompanionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (!grant.CanReadActivity || grant.HomeId != homeId) { throw new UnauthorizedAccessException("Activity requires its own home/member grant."); }
        lock (gate)
        {
            var now = Now();
            var records = Prune(grant.MemberId, now);
            var ordered = records.OrderBy(item => item.State is CompanionActivityState.Received or CompanionActivityState.Working ? 0 : 1)
                .ThenByDescending(item => item.UpdatedAt).ThenBy(item => item.RequestId, StringComparer.Ordinal).Take(20).ToArray();
            var coverageStart = now - Retention > startedAt ? now - Retention : startedAt;
            return new(homeId, grant.MemberId, now, coverageStart, enabled && members.Contains(grant.MemberId), gaps.Contains(grant.MemberId), records.Count > 20,
                Array.AsReadOnly(ordered));
        }
    }

    private CompanionActivityLease? Begin(IReadOnlyDictionary<string, string> mapping, string? sourceIdentity)
    {
        if (!enabled || sourceIdentity is null || !mapping.TryGetValue(sourceIdentity, out var memberId)) { return null; }
        lock (gate)
        {
            var now = Now(); var records = Prune(memberId, now);
            if (!requests.ContainsKey(memberId)) { requests.Add(memberId, records); }
            if (records.Count == 50)
            {
                var oldestFinished = records.FindIndex(item => item.State is not (CompanionActivityState.Received or CompanionActivityState.Working));
                records.RemoveAt(oldestFinished < 0 ? 0 : oldestFinished); gaps.Add(memberId);
            }
            var id = Guid.NewGuid().ToString("N");
            records.Add(new(id, CompanionActivityState.Received, now, now, 1));
            return new(id, state => Update(memberId, id, state));
        }
    }

    private void Update(string memberId, string id, CompanionActivityState state)
    {
        lock (gate)
        {
            if (!requests.TryGetValue(memberId, out var records)) { return; }
            var index = records.FindIndex(item => item.RequestId == id);
            if (index < 0 || records[index].State is not (CompanionActivityState.Received or CompanionActivityState.Working)) { return; }
            var previous = records[index];
            if (previous.State == state) { return; }
            records[index] = previous with { State = state, UpdatedAt = Now(), Revision = previous.Revision + 1 };
        }
    }

    private List<CompanionActivityItem> Prune(string memberId, DateTimeOffset now)
    {
        // Unknown members must not allocate retained storage from a read request.
        if (!requests.TryGetValue(memberId, out var records)) { return []; }
        records.RemoveAll(item => now - item.UpdatedAt >= Retention && item.State is not (CompanionActivityState.Received or CompanionActivityState.Working));
        return records;
    }

    // Anchor UTC once. Wall-clock corrections cannot extend retention or move a request backward in time.
    private DateTimeOffset Now() => startedAt + clock.GetElapsedTime(startedTimestamp);
}
