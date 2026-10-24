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

    [Theory]
    [InlineData(CompanionActivityState.Received)]
    [InlineData(CompanionActivityState.Working)]
    [InlineData(CompanionActivityState.Continued)]
    [InlineData((CompanionActivityState)999)]
    public void ACompletionCannotInventFollowUpEvidenceOrConsumeTheLeaseOnInvalidState(CompanionActivityState state)
    {
        var journal = Journal(); using var request = journal.BeginOwner("owner-a", "conversation");
        Assert.Throws<ArgumentException>(() => request!.Complete(state));
        Assert.Equal(CompanionActivityState.Received, Assert.Single(journal.Read(Grant()).Requests).State);
        request!.Complete(CompanionActivityState.ResponseReady);
        Assert.Equal(CompanionActivityState.ResponseReady, Assert.Single(journal.Read(Grant()).Requests).State);
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

    [Fact]
    public void AVerifiedLaterTurnEndsTheOldReplyRequestWithoutClaimingThatItWasAnswered()
    {
        var clock = Clock(); var journal = Journal(clock);
        using var first = journal.BeginOwner("owner-a", "PRIVATE_CONVERSATION");
        first!.Complete(CompanionActivityState.NeedsInput);
        var waiting = Assert.Single(journal.Read(Grant()).Requests);
        clock.Advance(TimeSpan.FromSeconds(2));
        using var next = journal.BeginOwner("owner-a", "PRIVATE_CONVERSATION");
        var after = journal.Read(Grant()).Requests.Single(item => item.RequestId == first.RequestId);
        Assert.Equal("Continued", after.State.ToString());
        Assert.Equal(waiting.Revision + 1, after.Revision);
        Assert.Equal(clock.GetUtcNow(), after.UpdatedAt);
        Assert.Equal(CompanionActivityState.Received, journal.Read(Grant()).Requests.Single(item => item.RequestId == next!.RequestId).State);
        Assert.DoesNotContain("PRIVATE_CONVERSATION", JsonSerializer.Serialize(journal.Read(Grant())));
        next!.Complete(CompanionActivityState.Failed);
        Assert.Equal("Continued", journal.Read(Grant()).Requests.Single(item => item.RequestId == first.RequestId).State.ToString());
    }

    [Theory]
    [InlineData("other-conversation", "owner-a", false)]
    [InlineData("conversation", "owner-other", false)]
    [InlineData("conversation", "profile-a", true)]
    [InlineData("conversation", "profile-b", true)]
    [InlineData("conversation", "unmapped", true)]
    public void AConversationSelectorCannotCrossVerifiedActorsSourceKindsOrConversations(string conversation, string actor, bool voice)
    {
        var settings = Settings(); settings.OwnerMembers["owner-other"] = "member-a";
        var journal = Journal(settings: settings);
        using var first = journal.BeginOwner("owner-a", "conversation"); first!.Complete(CompanionActivityState.NeedsInput);
        using var next = voice ? journal.BeginVoice(actor, conversation) : journal.BeginOwner(actor, conversation);
        Assert.Equal(CompanionActivityState.NeedsInput, journal.Read(Grant()).Requests.Single(item => item.RequestId == first.RequestId).State);
    }

    [Fact]
    public void OnlyOpaqueRequestIdsLinkRetainedTurnsWithoutPublishingTheSourceConversation()
    {
        var journal = Journal();
        using var first = journal.BeginOwner("owner-a", "PRIVATE_CONVERSATION"); first!.Complete(CompanionActivityState.ResponseReady);
        using var next = journal.BeginOwner("owner-a", "PRIVATE_CONVERSATION");
        var item = journal.Read(Grant()).Requests.Single(item => item.RequestId == next!.RequestId);
        var json = JsonSerializer.SerializeToElement(item);
        Assert.True(json.TryGetProperty("PreviousRequestId", out var previous));
        Assert.Equal(first.RequestId, previous.GetString());
        Assert.DoesNotContain("PRIVATE_CONVERSATION", json.ToString());
    }

    [Fact]
    public void AnOlderConcurrentTurnCannotReopenWaitingAfterALaterTurnWasAlreadySeenAndEvicted()
    {
        var clock = Clock(); var journal = Journal(clock);
        using var first = journal.BeginVoice("profile-a", "conversation"); first!.Working();
        using (var second = journal.BeginVoice("profile-a", "conversation")) { second!.Complete(CompanionActivityState.ResponseReady); }
        for (var i = 0; i < 60; i++)
        { clock.Advance(TimeSpan.FromSeconds(1)); using var unrelated = journal.BeginVoice("profile-a", "unrelated-" + i); unrelated!.Complete(CompanionActivityState.ResponseReady); }
        first.Complete(CompanionActivityState.NeedsInput);
        Assert.Equal("Continued", journal.Read(Grant()).Requests.Single(item => item.RequestId == first.RequestId).State.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n")]
    public void MissingConversationEvidenceNeverCorrelatesIndependentRequests(string? conversation)
    {
        var journal = Journal();
        using var first = journal.BeginOwner("owner-a", conversation); first!.Complete(CompanionActivityState.NeedsInput);
        using var next = journal.BeginOwner("owner-a", conversation);
        Assert.Equal(CompanionActivityState.NeedsInput, journal.Read(Grant()).Requests.Single(item => item.RequestId == first.RequestId).State);
    }

    [Fact]
    public void AnOversizedConversationSelectorIsIgnoredWithoutLosingTheAuthorizedActivity()
    {
        var journal = Journal(); var oversized = new string('x', 257);
        using var first = journal.BeginOwner("owner-a", oversized); first!.Complete(CompanionActivityState.NeedsInput);
        using var next = journal.BeginOwner("owner-a", oversized);
        Assert.NotNull(next);
        Assert.Equal(CompanionActivityState.NeedsInput, journal.Read(Grant()).Requests.Single(item => item.RequestId == first.RequestId).State);
    }

    [Fact]
    public void ARecentReplyRequestIsPrioritizedOverCompletedChatterUntilALaterTurnArrives()
    {
        var clock = Clock(); var journal = Journal(clock);
        using var waiting = journal.BeginOwner("owner-a", "waiting"); waiting!.Complete(CompanionActivityState.NeedsInput);
        for (var i = 0; i < 60; i++)
        { clock.Advance(TimeSpan.FromSeconds(1)); using var other = journal.BeginOwner("owner-a", "other"); other!.Complete(CompanionActivityState.ResponseReady); }
        Assert.Equal(waiting.RequestId, journal.Read(Grant()).Requests[0].RequestId);
        clock.Advance(TimeSpan.FromMinutes(31));
        Assert.DoesNotContain(journal.Read(Grant()).Requests, item => item.RequestId == waiting.RequestId);
    }

    internal static CompanionGrant Grant(string member = "member-a") => new("home-a", member, [], canReadActivity: true);
    internal static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
    internal static CompanionActivityOptions Settings() => new() { Enabled = true, VoiceMembers = new() { ["profile-a"] = "member-a", ["profile-b"] = "member-b" }, OwnerMembers = new() { ["owner-a"] = "member-a" } };
    private static CompanionActivityJournal Journal(FakeTimeProvider? clock = null, CompanionActivityOptions? settings = null) =>
        new(Options.Create(new CompanionOptions { Enabled = true, HomeId = "home-a" }), Options.Create(settings ?? Settings()), clock ?? Clock());
}
