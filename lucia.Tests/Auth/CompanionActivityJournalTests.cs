using System.Text.Json;
using lucia.AgentHost.Companion;
using lucia.Agents.Companion;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests.Auth;

public sealed class CompanionActivityJournalTests
{
    [Fact]
    public void ExplicitAttributionProducesOneStableRequestWithoutSourceIdentityOrPrivateContent()
    {
        var clock = Clock();
        var journal = Journal(clock);
        using var request = journal.BeginVoice("profile-a");
        Assert.NotNull(request);
        var received = Assert.Single(journal.Read(Grant()).Requests);
        Assert.Equal(request.RequestId, received.RequestId);
        Assert.Matches("^[a-f0-9]{32}$", received.RequestId);
        Assert.Equal(CompanionActivityState.Received, received.State);
        clock.Advance(TimeSpan.FromSeconds(2)); request.Working();
        var working = Assert.Single(journal.Read(Grant()).Requests);
        Assert.Equal(received.RequestId, working.RequestId);
        Assert.True(working.Revision > received.Revision);
        request.Complete(CompanionActivityState.ResponseReady);
        var finished = Assert.Single(journal.Read(Grant()).Requests);
        Assert.Equal(CompanionActivityState.ResponseReady, finished.State);
        Assert.True(finished.Revision > working.Revision);
        var wire = JsonSerializer.Serialize(journal.Read(Grant()));
        foreach (var forbidden in new[] { "profile-a", "owner-a", "transcript", "tool", "responseText", "conversationId", "placeId" })
        { Assert.DoesNotContain(forbidden, wire); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    [InlineData("Alice")]
    [InlineData("member-a")]
    public void UnverifiedOrUnmappedIdentityCreatesNoRequest(string? sourceIdentity)
    {
        var journal = Journal();
        Assert.Null(journal.BeginVoice(sourceIdentity));
        Assert.Null(journal.BeginOwner(sourceIdentity));
        Assert.Empty(journal.Read(Grant()).Requests);
    }

    [Fact]
    public void DisabledCollectionCannotBeEnabledByMappingsOrAReadGrant()
    {
        var options = Settings(); options.Enabled = false;
        var journal = Journal(settings: options);
        Assert.Null(journal.BeginVoice("profile-a"));
        Assert.False(journal.Read(Grant()).IsConfigured);
        var disabledHome = new CompanionActivityJournal(Options.Create(new CompanionOptions { Enabled = false, HomeId = "home-a" }), Options.Create(Settings()), Clock());
        Assert.Null(disabledHome.BeginOwner("owner-a"));
        Assert.False(disabledHome.Read(Grant()).IsConfigured);
    }

    [Fact]
    public void DifferentMembersNeverSeeEachOthersRecordsCountsOrEvictionGaps()
    {
        var journal = Journal();
        using var own = journal.BeginOwner("owner-a");
        for (var i = 0; i < 60; i++) { using var other = journal.BeginVoice("profile-b"); other!.Working(); }
        var ownRead = journal.Read(Grant());
        Assert.Single(ownRead.Requests);
        Assert.False(ownRead.HasMore); Assert.False(ownRead.HasGap);
        Assert.Equal(own!.RequestId, ownRead.Requests[0].RequestId);
        var otherRead = journal.Read(Grant("member-b"));
        Assert.Equal(20, otherRead.Requests.Count);
        Assert.True(otherRead.HasMore); Assert.True(otherRead.HasGap);
        Assert.DoesNotContain(otherRead.Requests, item => item.RequestId == own.RequestId);
    }

    [Fact]
    public void CrossHomeOrMissingActivityAuthorityIsRejectedEvenWithNoRecords()
    {
        var journal = Journal();
        Assert.Throws<UnauthorizedAccessException>(() => journal.Read(new("home-a", "member-a", [])));
        Assert.Throws<UnauthorizedAccessException>(() => journal.Read(new("home-b", "member-a", [], canReadActivity: true)));
        var unmapped = journal.Read(Grant("unmapped-member"));
        Assert.False(unmapped.IsConfigured); Assert.Empty(unmapped.Requests);
    }

    [Theory]
    [InlineData(CompanionActivityState.NeedsInput)]
    [InlineData(CompanionActivityState.ResponseReady)]
    [InlineData(CompanionActivityState.Failed)]
    [InlineData(CompanionActivityState.OutcomeUnknown)]
    public void FinalStatesAreImmutableAndDisposeCannotManufactureSuccess(CompanionActivityState state)
    {
        var journal = Journal();
        var request = journal.BeginVoice("profile-a")!;
        request.Working(); request.Complete(state);
        var revision = Assert.Single(journal.Read(Grant()).Requests).Revision;
        request.Complete(CompanionActivityState.ResponseReady); request.Working(); request.Dispose(); request.Dispose();
        var result = Assert.Single(journal.Read(Grant()).Requests);
        Assert.Equal(state, result.State); Assert.Equal(revision, result.Revision);
    }

    [Fact]
    public void DisposingAnUnfinishedRequestRecordsOnlyAnUnknownOutcome()
    {
        var journal = Journal();
        var request = journal.BeginVoice("profile-a")!;
        request.Working(); request.Dispose();
        Assert.Equal(CompanionActivityState.OutcomeUnknown, Assert.Single(journal.Read(Grant()).Requests).State);
    }

    [Fact]
    public void RetentionAndAProcessRestartCannotPretendToProvideCompleteHistory()
    {
        var clock = Clock(); var journal = Journal(clock);
        using (var request = journal.BeginVoice("profile-a")) { request!.Complete(CompanionActivityState.ResponseReady); }
        var started = journal.Read(Grant()).CoverageStartedAt;
        clock.Advance(TimeSpan.FromMinutes(31));
        var expired = journal.Read(Grant());
        Assert.Empty(expired.Requests); Assert.True(expired.CoverageStartedAt > started);
        var restarted = Journal(clock).Read(Grant());
        Assert.Empty(restarted.Requests); Assert.Equal(clock.GetUtcNow(), restarted.CoverageStartedAt);
    }

    [Fact]
    public void OptionsAreValidatedAndCopiedInsteadOfAllowingInPlaceAttributionChanges()
    {
        var settings = Settings(); var journal = Journal(settings: settings);
        settings.VoiceMembers["profile-a"] = "member-b";
        using var request = journal.BeginVoice("profile-a");
        Assert.Single(journal.Read(Grant()).Requests);
        Assert.Empty(journal.Read(Grant("member-b")).Requests);
        settings.VoiceMembers["invalid member key\n"] = "member-a";
        Assert.Throws<ArgumentException>(settings.Validate);
    }

    [Fact]
    public void SnapshotRecordsAndTheirListCannotMutateTheJournal()
    {
        var journal = Journal(); using var request = journal.BeginVoice("profile-a");
        var first = journal.Read(Grant());
        if (first.Requests is IList<CompanionActivityItem> list) { Assert.Throws<NotSupportedException>(() => list.Clear()); }
        request!.Working();
        Assert.Equal(CompanionActivityState.Received, first.Requests[0].State);
        Assert.Equal(CompanionActivityState.Working, journal.Read(Grant()).Requests[0].State);
    }

    [Fact]
    public void AWorkingRequestIsNotDisplacedByABurstOfCompletedRequests()
    {
        var clock = Clock(); var journal = Journal(clock);
        using var working = journal.BeginVoice("profile-a"); working!.Working();
        for (var i = 0; i < 60; i++)
        { clock.Advance(TimeSpan.FromSeconds(1)); using var request = journal.BeginOwner("owner-a"); request!.Complete(CompanionActivityState.ResponseReady); }
        var snapshot = journal.Read(Grant());
        Assert.Equal(working.RequestId, snapshot.Requests[0].RequestId);
        Assert.Equal(CompanionActivityState.Working, snapshot.Requests[0].State);
        Assert.True(snapshot.HasGap); Assert.True(snapshot.HasMore);
    }

    [Fact]
    public void AStillProcessingRequestCanOutliveTheRecentCompletedWindowWithoutInventingANewStatusTime()
    {
        var clock = Clock(); var journal = Journal(clock);
        using var working = journal.BeginVoice("profile-a"); working!.Working();
        var before = Assert.Single(journal.Read(Grant()).Requests);
        clock.Advance(TimeSpan.FromMinutes(31));
        var after = Assert.Single(journal.Read(Grant()).Requests);
        Assert.Equal(before.RequestId, after.RequestId); Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(before.Revision, after.Revision);
        working.Complete(CompanionActivityState.ResponseReady);
        Assert.Equal(CompanionActivityState.ResponseReady, Assert.Single(journal.Read(Grant()).Requests).State);
    }

    internal static CompanionGrant Grant(string member = "member-a") => new("home-a", member, [], canReadActivity: true);
    internal static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
    internal static CompanionActivityOptions Settings() => new() { Enabled = true, VoiceMembers = new() { ["profile-a"] = "member-a", ["profile-b"] = "member-b" }, OwnerMembers = new() { ["owner-a"] = "member-a" } };
    private static CompanionActivityJournal Journal(FakeTimeProvider? clock = null, CompanionActivityOptions? settings = null) =>
        new(Options.Create(new CompanionOptions { Enabled = true, HomeId = "home-a" }), Options.Create(settings ?? Settings()), clock ?? Clock());
}
