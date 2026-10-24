using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FakeItEasy;
using lucia.AgentHost;
using lucia.AgentHost.Apis;
using lucia.AgentHost.Companion;
using lucia.AgentHost.Conversation;
using lucia.AgentHost.Conversation.Execution;
using lucia.AgentHost.Conversation.Models;
using lucia.AgentHost.Conversation.Templates;
using lucia.AgentHost.Conversation.Tracing;
using lucia.Agents.CommandTracing;
using lucia.Agents.Orchestration;
using lucia.Wyoming.CommandRouting;
using Microsoft.Extensions.Logging.Abstractions;
using Lucia.Companion.Connection;

namespace lucia.Tests.Auth;

/// <summary>Real conversation/controller/auth/HTTPS/SQLite, synthetic command executor only. No model or household actions.</summary>
public sealed class CompanionActivityConversationWireTests
{
    [Fact]
    public async Task CurrentOwnerConversationProducesOnlyItsMembersRealLifecycleThroughTheProtectedFeed()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<CommandRouteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var router = A.Fake<ICommandRouter>();
        A.CallTo(() => router.RouteAsync(A<string>._, A<CancellationToken>._)).ReturnsLazily(() => { entered.TrySetResult(); return release.Task; });
        await using var host = await StartAsync(router);
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        var issued = await host.Sessions.CreateAsync(CompanionActivityJournalTests.Grant(), "Phone");
        using var device = Device(host, issued.AccessToken);
        var directory = Directory.CreateTempSubdirectory("lucia-activity-native-wire-");
        var vault = new ProtectedSessionVault(directory.FullName, CompanionCoordinatorIntegrationTests.EphemeralProtector());
        using var seedApi = host.CreateNativeClient();
        await new DeviceSessionCoordinator(seedApi, vault).ConnectAsync(new DeviceCredentials(issued.SessionId, issued.AccessToken,
            issued.AccessExpiresAt, issued.RefreshToken, issued.RefreshExpiresAt));
        using var native = new CompanionConnection(vault, _ => host.CreateNativeClient());
        var identity = (await native.RestoreAsync())!;
        var protectedBefore = await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "session.bin"));
        var pending = host.Client.PostAsJsonAsync("/api/conversation/", new
        {
            text = "PRIVATE_WIRE_REQUEST", context = new { conversationId = "PRIVATE_CONVERSATION", userId = "member-b", activityOwnerKeyId = "owner-b", enrolledProfileId = "profile-b", deviceArea = "PRIVATE_ROOM" }
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var during = await device.GetAsync("/api/companion/homes/home-a/activity");
            Assert.Equal(HttpStatusCode.OK, during.StatusCode);
            var duringText = await during.Content.ReadAsStringAsync();
            Assert.DoesNotContain("PRIVATE_", duringText);
            var working = JsonDocument.Parse(duringText).RootElement.GetProperty("requests").EnumerateArray().Single();
            var id = working.GetProperty("requestId").GetString();
            Assert.Equal("Working", working.GetProperty("state").GetString());
            var nativeWorking = Assert.Single((await native.ReadActivityAsync(identity)).Requests);
            Assert.Equal(RequestActivityState.Working, nativeWorking.State);
            Assert.Equal(id, nativeWorking.RequestId);
            release.SetResult(Route());
            using var completed = await pending;
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
            var after = await device.GetFromJsonAsync<JsonElement>("/api/companion/homes/home-a/activity");
            var ready = Assert.Single(after.GetProperty("requests").EnumerateArray());
            Assert.Equal(id, ready.GetProperty("requestId").GetString());
            Assert.Equal("ResponseReady", ready.GetProperty("state").GetString());
            Assert.DoesNotContain("PRIVATE_", after.ToString());
            Assert.Equal(RequestActivityState.ResponseReady, Assert.Single((await native.ReadActivityAsync(identity)).Requests).State);
            using var next = await host.Client.PostAsJsonAsync("/api/conversation/", new
                { text = "PRIVATE_REPLY", context = new { conversationId = "PRIVATE_CONVERSATION" } });
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
            var correlated = (await native.ReadActivityAsync(identity)).Requests.Single(item => item.RequestId != id);
            Assert.Equal(id, correlated.PreviousRequestId);
            Assert.Equal(RequestActivityState.ResponseReady, correlated.State);
            Assert.Equal(protectedBefore, await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "session.bin")));

            var other = await host.Sessions.CreateAsync(CompanionActivityJournalTests.Grant("member-b"), "Tablet");
            using var otherDevice = Device(host, other.AccessToken);
            Assert.Empty((await otherDevice.GetFromJsonAsync<JsonElement>("/api/companion/homes/home-a/activity")).GetProperty("requests").EnumerateArray());
            // Read permission must not become a conversation/physical-command capability.
            Assert.Equal(HttpStatusCode.Unauthorized, (await device.PostAsJsonAsync("/api/conversation/", new { text = "PRIVATE_", context = new { } })).StatusCode);
            await host.Sessions.RevokeAsync(issued.SessionId, "home-a");
            Assert.Equal(ConnectionFailure.Unauthorized, (await Assert.ThrowsAsync<CompanionConnectionException>(() => native.ReadActivityAsync(identity))).Failure);
            Assert.False(native.HasSavedConnection);
            await using var cleared = await vault.OpenAsync(); Assert.Null(await cleared.ReadAsync());
        }
        finally { release.TrySetResult(Route()); using var finished = await pending; directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task UnverifiedBodyIdentityWithAnOwnerApiKeyDoesNotAttributeAConversationToItsClaimedMember()
    {
        var router = A.Fake<ICommandRouter>();
        A.CallTo(() => router.RouteAsync(A<string>._, A<CancellationToken>._)).Returns(Route());
        await using var host = await StartAsync(router);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        using var result = await host.Client.PostAsJsonAsync("/api/conversation/", new
        {
            text = "PRIVATE_", context = new { userId = "owner-a", isDashboardSession = true, activityOwnerKeyId = "owner-a", enrolledProfileId = "profile-a" }
        });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Empty(host.Activity.Read(CompanionActivityJournalTests.Grant()).Requests);
    }

    [Fact]
    public async Task MissingEngineStreamingFallbackEndsAsFailedRatherThanAnInventedSuccessfulResponse()
    {
        var router = A.Fake<ICommandRouter>();
        A.CallTo(() => router.RouteAsync(A<string>._, A<CancellationToken>._)).Returns(CommandRouteResult.NoMatch(TimeSpan.Zero));
        await using var host = await StartAsync(router);
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        using var result = await host.Client.PostAsJsonAsync("/api/conversation/", new { text = "PRIVATE_", context = new { } });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("LLM engine not available", await result.Content.ReadAsStringAsync());
        Assert.Equal(CompanionActivityState.Failed, Assert.Single(host.Activity.Read(CompanionActivityJournalTests.Grant()).Requests).State);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DisabledOrUnmappedCollectionDoesNotAddAnOwnerDatabaseReadToExistingConversations(bool enabled, bool mapped)
    {
        var router = A.Fake<ICommandRouter>();
        A.CallTo(() => router.RouteAsync(A<string>._, A<CancellationToken>._)).Returns(Route());
        await using var host = await StartAsync(router, enabled, mapped);
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        using var result = await host.Client.PostAsJsonAsync("/api/conversation/", new { text = "PRIVATE_", context = new { } });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        // Existing cookie authentication checks its owner key once. An unused optional feed adds no second read.
        A.CallTo(() => host.Keys.GetKeyAsync("owner-a", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.Empty(host.Activity.Read(CompanionActivityJournalTests.Grant()).Requests);
    }

    private static Task<CompanionSecurityTestHost> StartAsync(ICommandRouter router, bool enabled = true, bool mapped = true) => CompanionSecurityTestHost.StartAsync(
        configureApp: app => app.MapConversationApi(), configureServices: services =>
        {
            services.Configure<CompanionActivityOptions>(settings =>
            { settings.Enabled = enabled; if (mapped) { settings.OwnerMembers["owner-a"] = "member-a"; } });
            services.Configure<CommandRoutingOptions>(_ => { });
            services.Configure<PersonalityPromptOptions>(settings => settings.UsePersonalityResponses = false);
            services.AddSingleton(router);
            var executor = A.Fake<IDirectSkillExecutor>();
            A.CallTo(() => executor.ExecuteAsync(A<CommandRouteResult>._, A<ConversationContext>._, A<CancellationToken>._))
                .Returns(new SkillExecutionResult { Success = true, SkillId = "synthetic", Action = "read", ResponseText = "PRIVATE_WIRE_RESPONSE" });
            services.AddSingleton(executor);
            var templates = A.Fake<IResponseTemplateRepository>();
            A.CallTo(() => templates.GetBySkillAndActionAsync("synthetic", "read", A<CancellationToken>._))
                .Returns(new ResponseTemplate { SkillId = "synthetic", Action = "read", Templates = ["PRIVATE_WIRE_RESPONSE"] });
            services.AddSingleton(new ResponseTemplateRenderer(templates, NullLogger<ResponseTemplateRenderer>.Instance));
            services.AddSingleton<ContextReconstructor>(); services.AddSingleton<AgentHostTelemetrySource>(); services.AddSingleton<ConversationTelemetry>();
            services.AddSingleton<ICommandTraceRepository, InMemoryCommandTraceRepository>(); services.AddSingleton<CommandTraceChannel>();
            services.AddSingleton<ConversationCommandProcessor>();
        });

    private static CommandRouteResult Route() => new() { IsMatch = true, Confidence = 1,
        MatchedPattern = new() { Id = "synthetic", SkillId = "synthetic", Action = "read", Templates = ["PRIVATE_"] } };
    private static HttpClient Device(CompanionSecurityTestHost host, string access)
    {
        var fingerprint = host.CertificateFingerprint;
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString(HashAlgorithmName.SHA256) == fingerprint })
            { BaseAddress = host.Client.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new("Bearer", access);
        return client;
    }
}
