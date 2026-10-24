using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Claims;

using lucia.AgentHost.Conversation;
using lucia.AgentHost.Conversation.Models;
using lucia.AgentHost.Companion;
using lucia.Agents.Abstractions;
using lucia.Agents.Orchestration;
using lucia.Wyoming.CommandRouting;

using Microsoft.AspNetCore.Mvc;

namespace lucia.AgentHost.Apis;

/// <summary>
/// Maps the /api/conversation endpoint for Home Assistant's fast-path conversation pipeline.
/// </summary>
public static class ConversationApi
{
    private static readonly JsonSerializerOptions SSseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Registers the POST /api/conversation endpoint.
    /// Returns JSON for command-parsed results, SSE for LLM fallback.
    /// </summary>
    public static IEndpointRouteBuilder MapConversationApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/conversation")
            .WithTags("Conversation");

        group.MapPost("/", HandleConversationAsync).RequireAuthorization();
        group.MapGet("/patterns", GetCommandPatternsAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task HandleConversationAsync(
        HttpContext httpContext,
        [FromBody] ConversationRequest request,
        [FromServices] ConversationCommandProcessor processor,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] IApiKeyService keys,
        [FromServices] TimeProvider clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                new { error = "Text is required" }, ct).ConfigureAwait(false);
            return;
        }

        var dashboardSession = httpContext.User.FindFirst("auth_method")?.Value == "session";
        var candidateOwner = dashboardSession ? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        var ownerId = serviceProvider.GetService<CompanionActivityJournal>()?.HasOwnerMapping(candidateOwner) == true
            && await CompanionOwnerFilter.HasCurrentOwnerAsync(httpContext, keys, clock).ConfigureAwait(false) ? candidateOwner : null;
        request = request with
        {
            Context = request.Context with
            {
                IsDashboardSession = dashboardSession,
                ActivityOwnerKeyId = ownerId,
            },
        };
        var result = await processor.ProcessAsync(request, ct).ConfigureAwait(false);
        using var tracked = result.Activity;

        switch (result.Kind)
        {
            case ProcessingKind.CommandHandled:
            case ProcessingKind.LlmCompleted:
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                await httpContext.Response.WriteAsJsonAsync(result.Response, ct).ConfigureAwait(false);
                break;

            case ProcessingKind.LlmFallback:
                await StreamLlmResponseAsync(
                    httpContext, result, serviceProvider, tracked, ct).ConfigureAwait(false);
                break;
        }
    }

    private static IResult GetCommandPatternsAsync(
        [FromServices] CommandPatternRegistry registry)
    {
        var patterns = registry.GetAllPatterns()
            .Select(p => new
            {
                p.SkillId,
                p.Action,
                PatternId = p.Id,
                Tokens = ExtractTokens(p.Templates),
                ExampleTemplates = p.Templates,
            })
            .ToList();

        return Results.Ok(patterns);
    }

    private static string[] ExtractTokens(IReadOnlyList<string> templates)
    {
        return templates
            .SelectMany(t => Regex.Matches(t, @"\{(\w+)(?::[^}]*)?\}"))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task StreamLlmResponseAsync(
        HttpContext httpContext,
        ProcessingResult result,
        IServiceProvider serviceProvider,
        CompanionActivityLease? tracked,
        CancellationToken ct)
    {
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        // Send metadata event
        await WriteSseEventAsync(httpContext, "metadata", new
        {
            type = "llm",
            conversationId = result.ConversationId,
        }, ct).ConfigureAwait(false);

        // Execute LLM request
        var engine = serviceProvider.GetService<LuciaEngine>();
        if (engine is null || string.IsNullOrEmpty(result.LlmPrompt))
        {
            tracked?.Complete(CompanionActivityState.Failed);
            await WriteSseEventAsync(httpContext, "error", new
            {
                error = "LLM engine not available",
            }, ct).ConfigureAwait(false);
            return;
        }

        try
        {
            var llmResult = await engine
                .ProcessRequestAsync(
                    result.LlmPrompt,
                    sessionId: result.EngineSessionId ?? result.ConversationId,
                    speakerContext: result.SpeakerContext,
                    originalUserText: result.OriginalUserText,
                    cancellationToken: ct)
                .ConfigureAwait(false);
            tracked?.Complete(llmResult.NeedsInput ? CompanionActivityState.NeedsInput : CompanionActivityState.ResponseReady);

            // Send the complete response as a done event
            await WriteSseEventAsync(httpContext, "done", new
            {
                text = llmResult.Text,
                conversationId = result.ConversationId,
                needsInput = llmResult.NeedsInput,
            }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected; stop streaming without surfacing an error.
            return;
        }
        catch (Exception ex)
        {
            tracked?.Complete(CompanionActivityState.Failed);
            await WriteSseEventAsync(httpContext, "error", new
            {
                error = ex.Message,
            }, ct).ConfigureAwait(false);
        }
    }

    private static async Task WriteSseEventAsync(
        HttpContext httpContext, string eventType, object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data, SSseJsonOptions);
        var line = $"event: {eventType}\ndata: {json}\n\n";
        await httpContext.Response.WriteAsync(line, ct).ConfigureAwait(false);
        await httpContext.Response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}
