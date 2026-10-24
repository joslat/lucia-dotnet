using System.Text.Json;
using FakeItEasy;
using lucia.AgentHost;
using lucia.AgentHost.Companion;
using lucia.AgentHost.Conversation;
using lucia.AgentHost.Conversation.Execution;
using lucia.AgentHost.Conversation.Models;
using lucia.AgentHost.Conversation.Templates;
using lucia.AgentHost.Conversation.Tracing;
using lucia.Agents.Companion;
using lucia.Agents.Orchestration;
using lucia.Tests.Auth;
using lucia.Wyoming.CommandRouting;
using lucia.Wyoming.Diarization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace lucia.Tests.Conversation;

public sealed class CompanionConversationActivityTests : IDisposable
{
    private readonly ICommandRouter router = A.Fake<ICommandRouter>();
    private readonly IDirectSkillExecutor executor = A.Fake<IDirectSkillExecutor>();
    private readonly ISpeakerProfileStore profiles = A.Fake<ISpeakerProfileStore>();
    private readonly VoiceTurnStore turns = new();
    private readonly StickySpeakerStore sticky = new();
    private readonly AgentHostTelemetrySource telemetry = new();
    private readonly CompanionActivityJournal journal = new(Options.Create(new CompanionOptions { Enabled = true, HomeId = "home-a" }),
        Options.Create(CompanionActivityJournalTests.Settings()), TimeProvider.System);

    public CompanionConversationActivityTests()
    {
        A.CallTo(() => router.RouteAsync(A<string>._, A<CancellationToken>._)).Returns(Route());
        A.CallTo(() => executor.ExecuteAsync(A<CommandRouteResult>._, A<ConversationContext>._, A<CancellationToken>._))
            .Returns(new SkillExecutionResult { Success = true, SkillId = "synthetic", Action = "read", ResponseText = "PRIVATE_RESPONSE" });
        A.CallTo(() => profiles.GetAsync("profile-a", A<CancellationToken>._))
            .Returns(new SpeakerProfile { Id = "profile-a", Name = "PRIVATE_NAME", IsAuthorized = true, IsProvisional = false });
    }

    [Fact]
    public async Task CurrentDashboardIdentityTracksTheRealProcessingIntervalThenItsResponse()
    {
        var routing = new TaskCompletionSource<CommandRouteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => router.RouteAsync(A<string>._, A<CancellationToken>._)).Returns(routing.Task);
        var pending = Processor().ProcessAsync(new() { Text = "PRIVATE_REQUEST", Context = new() { IsDashboardSession = true, ActivityOwnerKeyId = "owner-a" } });
        try
        {
            var working = Assert.Single(journal.Read(CompanionActivityJournalTests.Grant()).Requests);
            Assert.Equal(CompanionActivityState.Working, working.State);
            routing.SetResult(Route());
            await pending;
            var ready = Assert.Single(journal.Read(CompanionActivityJournalTests.Grant()).Requests);
            Assert.Equal(working.RequestId, ready.RequestId);
            Assert.Equal(CompanionActivityState.ResponseReady, ready.State);
            Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(journal.Read(CompanionActivityJournalTests.Grant())));
        }
        finally { routing.TrySetResult(Route()); await pending; }
    }

    [Fact]
    public async Task VerifiedActorTargetsReachPrivateProjectionBeforeTheSkillCompletes()
    {
        var completion = new TaskCompletionSource<SkillExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => executor.ExecuteAsync(A<CommandRouteResult>._, A<ConversationContext>._, A<CancellationToken>._))
            .Invokes((CommandRouteResult _, ConversationContext context, CancellationToken _) => context.RequestedTargetsObserver?.Invoke(["light.porch"]))
            .Returns(completion.Task);
        var pending = Processor().ProcessAsync(new() { Text = "PRIVATE_REQUEST", Context = new() { IsDashboardSession = true, ActivityOwnerKeyId = "owner-a" } });
        try
        {
            var projection = journal.ReadForProjection(CompanionActivityJournalTests.Grant());
            var working = Assert.Single(projection.Snapshot.Requests);
            Assert.Equal(CompanionActivityState.Working, working.State);
            Assert.Equal(["light.porch"], projection.TargetEntityIds[working.RequestId]);
            Assert.Empty(journal.ReadForProjection(CompanionActivityJournalTests.Grant("member-b")).TargetEntityIds);
            Assert.DoesNotContain("light.porch", JsonSerializer.Serialize(projection.Snapshot));
        }
        finally
        {
            completion.TrySetResult(new() { Success = true, SkillId = "synthetic", Action = "read", ResponseText = "PRIVATE_RESPONSE" });
            await pending;
        }
    }

    [Fact]
    public async Task FreshAuthorizedVoiceProofOverridesUntrustedBodyIdentityAndNeverCopiesTextOrNames()
    {
        var request = VoiceRequest(new() { ProfileId = "profile-a", Name = "PRIVATE_NAME", Similarity = .95f, IsAuthorized = true });
        request = request with { Context = request.Context with { UserId = "member-b", SpeakerId = "owner-a", DeviceArea = "PRIVATE_ROOM" } };
        await Processor().ProcessAsync(request);
        Assert.Equal(CompanionActivityState.ResponseReady, Assert.Single(journal.Read(CompanionActivityJournalTests.Grant()).Requests).State);
        Assert.Empty(journal.Read(CompanionActivityJournalTests.Grant("member-b")).Requests);
        Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(journal.Read(CompanionActivityJournalTests.Grant())));
    }

    [Fact]
    public async Task StickySpeakerContinuityIsNotFreshProofForPublishingAnotherUtterance()
    {
        sticky.Remember("conversation", "profile-a");
        await Processor(stickyEnabled: true).ProcessAsync(VoiceRequest(null));
        Assert.Empty(journal.Read(CompanionActivityJournalTests.Grant()).Requests);
    }

    [Fact]
    public async Task TheVerifiedOwnerSourceCorrelatesALaterTurnBeforeCommandRoutingCompletes()
    {
        using var first = journal.BeginOwner("owner-a", "conversation"); first!.Complete(CompanionActivityState.NeedsInput);
        var routing = new TaskCompletionSource<CommandRouteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => router.RouteAsync(A<string>._, A<CancellationToken>._)).Returns(routing.Task);
        var pending = Processor().ProcessAsync(new() { Text = "PRIVATE_REPLY", Context = new()
            { IsDashboardSession = true, ActivityOwnerKeyId = "owner-a", ConversationId = "conversation" } });
        try
        {
            var snapshot = journal.Read(CompanionActivityJournalTests.Grant());
            Assert.Equal(CompanionActivityState.Continued, snapshot.Requests.Single(item => item.RequestId == first.RequestId).State);
            Assert.Equal(first.RequestId, snapshot.Requests.Single(item => item.State == CompanionActivityState.Working).PreviousRequestId);
        }
        finally { routing.TrySetResult(Route()); await pending; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyThisTurnsVerifiedVoiceCanContinueItsPriorReplyRequest(bool freshProof)
    {
        using var first = journal.BeginVoice("profile-a", "conversation"); first!.Complete(CompanionActivityState.NeedsInput);
        sticky.Remember("conversation", "profile-a");
        await Processor(stickyEnabled: true).ProcessAsync(VoiceRequest(freshProof
            ? new() { ProfileId = "profile-a", Name = "PRIVATE_NAME", Similarity = .95f, IsAuthorized = true } : null));
        var before = journal.Read(CompanionActivityJournalTests.Grant()).Requests.Single(item => item.RequestId == first.RequestId);
        Assert.Equal(freshProof ? CompanionActivityState.Continued : CompanionActivityState.NeedsInput, before.State);
    }

    [Theory]
    [InlineData(false, false, .95f)]
    [InlineData(true, true, .95f)]
    [InlineData(true, false, float.NaN)]
    public async Task UnauthorizedProvisionalOrInvalidVoiceProofCreatesNoActivity(bool authorized, bool provisional, float similarity)
    {
        A.CallTo(() => profiles.GetAsync("profile-a", A<CancellationToken>._))
            .Returns(new SpeakerProfile { Id = "profile-a", Name = "PRIVATE_NAME", IsAuthorized = authorized, IsProvisional = provisional });
        await Processor().ProcessAsync(VoiceRequest(new() { ProfileId = "profile-a", Name = "PRIVATE_NAME", Similarity = similarity, IsAuthorized = true }));
        Assert.Empty(journal.Read(CompanionActivityJournalTests.Grant()).Requests);
    }

    [Fact]
    public async Task BodyFieldsCannotForgeDashboardOrVerifiedVoiceAuthority()
    {
        var request = JsonSerializer.Deserialize<ConversationRequest>("""
            {"text":"PRIVATE_REQUEST", "context":{"userId":"owner-a", "speakerId":"profile-a", "deviceArea":"porch",
             "activityOwnerKeyId":"owner-a", "isDashboardSession":true, "enrolledProfileId":"profile-a", "requestedTargetsObserver":"PRIVATE_CALLBACK"}}
            """)!;
        Assert.Null(request.Context.ActivityOwnerKeyId); Assert.False(request.Context.IsDashboardSession);
        Assert.Null(request.Context.RequestedTargetsObserver);
        await Processor().ProcessAsync(request);
        Assert.Empty(journal.Read(CompanionActivityJournalTests.Grant()).Requests);
    }

    [Fact]
    public async Task ARealProcessingExceptionCannotPublishAResponseReadyOrLeakItsMessage()
    {
        A.CallTo(() => router.RouteAsync(A<string>._, A<CancellationToken>._)).ThrowsAsync(new InvalidOperationException("PRIVATE_ERROR"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Processor().ProcessAsync(new()
        { Text = "PRIVATE_REQUEST", Context = new() { IsDashboardSession = true, ActivityOwnerKeyId = "owner-a" } }));
        Assert.Equal(CompanionActivityState.Failed, Assert.Single(journal.Read(CompanionActivityJournalTests.Grant()).Requests).State);
        Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(journal.Read(CompanionActivityJournalTests.Grant())));
    }

    private ConversationCommandProcessor Processor(bool stickyEnabled = false)
    {
        var services = A.Fake<IServiceProvider>(); A.CallTo(() => services.GetService(A<Type>._)).Returns(null);
        var templates = A.Fake<IResponseTemplateRepository>();
        A.CallTo(() => templates.GetBySkillAndActionAsync("synthetic", "read", A<CancellationToken>._))
            .Returns(new ResponseTemplate { SkillId = "synthetic", Action = "read", Templates = ["PRIVATE_RESPONSE"] });
        return new(router, executor, new ResponseTemplateRenderer(templates, NullLogger<ResponseTemplateRenderer>.Instance),
            new ContextReconstructor(), new ConversationTelemetry(telemetry), new InMemoryCommandTraceRepository(), new CommandTraceChannel(), services,
            NullLogger<ConversationCommandProcessor>.Instance, Monitor(new CommandRoutingOptions()), Monitor(new PersonalityPromptOptions()),
            voiceTurns: turns, speakerProfiles: profiles, stickySpeakers: sticky,
            voiceProfileOptions: Monitor(new VoiceProfileOptions { StickySpeakerFollowUps = stickyEnabled }), companionActivity: journal);
    }
    private ConversationRequest VoiceRequest(SpeakerIdentification? speaker) => new()
    {
        Text = turns.Capture("PRIVATE_REQUEST", ReadOnlyMemory<float>.Empty, 1600, speaker),
        Context = new() { ConversationId = "conversation", DeviceId = "satellite" }
    };
    private static CommandRouteResult Route() => new() { IsMatch = true, Confidence = 1,
        MatchedPattern = new() { Id = "synthetic", SkillId = "synthetic", Action = "read", Templates = ["PRIVATE_REQUEST"] } };
    private static IOptionsMonitor<T> Monitor<T>(T value) where T : class
    { var monitor = A.Fake<IOptionsMonitor<T>>(); A.CallTo(() => monitor.CurrentValue).Returns(value); return monitor; }
    public void Dispose() { turns.Dispose(); telemetry.Dispose(); }
}
