using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private readonly Dictionary<string, List<CompanionActivityEntry>> requests = new(StringComparer.Ordinal);
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
    public CompanionActivityLease? BeginVoice(string? verifiedProfileId, string? conversationId = null) => Begin(voiceMembers, "voice", verifiedProfileId, conversationId);
    public CompanionActivityLease? BeginOwner(string? currentOwnerKeyId, string? conversationId = null) => Begin(ownerMembers, "owner", currentOwnerKeyId, conversationId);
    internal bool HasOwnerMapping(string? ownerKeyId) => enabled && ownerKeyId is not null && ownerMembers.ContainsKey(ownerKeyId);

    public CompanionActivitySnapshot Read(CompanionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (!grant.CanReadActivity || grant.HomeId != homeId) { throw new UnauthorizedAccessException("Activity requires its own home/member grant."); }
        lock (gate)
        {
            var now = Now();
            var records = Prune(grant.MemberId, now);
            var ordered = records.Select(entry => entry.Item).OrderBy(item => item.State switch
                { CompanionActivityState.Received or CompanionActivityState.Working => 0, CompanionActivityState.NeedsInput => 1, _ => 2 })
                .ThenByDescending(item => item.UpdatedAt).ThenBy(item => item.RequestId, StringComparer.Ordinal).Take(20).ToArray();
            var coverageStart = now - Retention > startedAt ? now - Retention : startedAt;
            return new(homeId, grant.MemberId, now, coverageStart, enabled && members.Contains(grant.MemberId), gaps.Contains(grant.MemberId), records.Count > 20,
                Array.AsReadOnly(ordered));
        }
    }

    private CompanionActivityLease? Begin(IReadOnlyDictionary<string, string> mapping, string sourceKind, string? sourceIdentity, string? conversationId)
    {
        if (!enabled || sourceIdentity is null || !mapping.TryGetValue(sourceIdentity, out var memberId)) { return null; }
        // The caller's conversation selector is never identity. It can correlate only within this verified actor.
        var key = !string.IsNullOrWhiteSpace(conversationId) && conversationId.Length <= 256 && !conversationId.Any(char.IsControl)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { sourceKind, sourceIdentity, conversationId })))) : null;
        lock (gate)
        {
            var now = Now(); var records = Prune(memberId, now);
            if (!requests.ContainsKey(memberId)) { requests.Add(memberId, records); }
            string? previousId = null;
            if (key is not null)
            {
                for (var index = 0; index < records.Count; index++)
                {
                    var previous = records[index];
                    if (previous.ConversationKey != key) { continue; }
                    previousId = previous.Item.RequestId;
                    var item = previous.Item.State == CompanionActivityState.NeedsInput
                        ? previous.Item with { State = CompanionActivityState.Continued, UpdatedAt = now, Revision = previous.Item.Revision + 1 }
                        : previous.Item;
                    records[index] = previous with { Item = item, HasLaterTurn = true };
                }
            }
            if (records.Count == 50)
            {
                var oldestFinished = records.FindIndex(entry => entry.Item.State is not (CompanionActivityState.Received or CompanionActivityState.Working or CompanionActivityState.NeedsInput));
                records.RemoveAt(oldestFinished < 0 ? 0 : oldestFinished); gaps.Add(memberId);
            }
            var id = Guid.NewGuid().ToString("N");
            records.Add(new(new(id, CompanionActivityState.Received, now, now, 1, previousId), key));
            return new(id, state => Update(memberId, id, state));
        }
    }

    private void Update(string memberId, string id, CompanionActivityState state)
    {
        lock (gate)
        {
            if (!requests.TryGetValue(memberId, out var records)) { return; }
            var index = records.FindIndex(entry => entry.Item.RequestId == id);
            if (index < 0 || records[index].Item.State is not (CompanionActivityState.Received or CompanionActivityState.Working)) { return; }
            var previous = records[index];
            if (state == CompanionActivityState.NeedsInput && previous.HasLaterTurn) { state = CompanionActivityState.Continued; }
            if (previous.Item.State == state) { return; }
            records[index] = previous with { Item = previous.Item with { State = state, UpdatedAt = Now(), Revision = previous.Item.Revision + 1 } };
        }
    }

    private List<CompanionActivityEntry> Prune(string memberId, DateTimeOffset now)
    {
        // Unknown members must not allocate retained storage from a read request.
        if (!requests.TryGetValue(memberId, out var records)) { return []; }
        records.RemoveAll(entry => now - entry.Item.UpdatedAt >= Retention && entry.Item.State is not (CompanionActivityState.Received or CompanionActivityState.Working));
        return records;
    }

    // Anchor UTC once. Wall-clock corrections cannot extend retention or move a request backward in time.
    private DateTimeOffset Now() => startedAt + clock.GetElapsedTime(startedTimestamp);
}
