using FakeItEasy;
using lucia.AgentHost;
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

namespace lucia.Tests.Auth;

/// <summary>Shared isolated fixture registration. Real processor/templates/tracing, synthetic nonphysical skill only.</summary>
public static class SyntheticCompanionConversationServices
{
    public static void Configure(IServiceCollection services, ICommandRouter router, bool enabled = true, bool mapped = true)
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
        A.CallTo(() => templates.GetBySkillAndActionAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string skill, string action, CancellationToken _) =>
                new ResponseTemplate { SkillId = skill, Action = action, Templates = ["PRIVATE_WIRE_RESPONSE"] });
        services.AddSingleton(new ResponseTemplateRenderer(templates, NullLogger<ResponseTemplateRenderer>.Instance));
        services.AddSingleton<ContextReconstructor>(); services.AddSingleton<AgentHostTelemetrySource>(); services.AddSingleton<ConversationTelemetry>();
        services.AddSingleton<ICommandTraceRepository, InMemoryCommandTraceRepository>(); services.AddSingleton<CommandTraceChannel>();
        services.AddSingleton<ConversationCommandProcessor>();
    }
}
