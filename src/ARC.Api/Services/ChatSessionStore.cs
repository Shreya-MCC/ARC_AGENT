using ARC.Api.Auth;
using ARC.Api.DTOs;
using ARC.Data.Cosmos;
using ARC.Data.Exceptions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace ARC.Api.Services;

/// <summary>Persists chat turns per session in the Cosmos knowledgeChunks container.</summary>
public sealed class ChatSessionStore
{
    private readonly IChatSessionRepository _repository;
    private readonly ILogger<ChatSessionStore> _logger;

    public ChatSessionStore(IChatSessionRepository repository, ILogger<ChatSessionStore> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<string> ResolveSessionIdAsync(
        string? sessionId,
        ArcActor actor,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId.Trim();

        var active = await _repository.GetActiveSessionIdAsync(actor.Upn, cancellationToken);
        if (!string.IsNullOrWhiteSpace(active))
            return active;

        return await CreateSessionAsync(actor, cancellationToken);
    }

    public async Task<ChatHistoryResponse> GetCurrentSessionAsync(
        ArcActor actor,
        CancellationToken cancellationToken)
    {
        var sessionId = await ResolveSessionIdAsync(sessionId: null, actor, cancellationToken);
        return await GetHistoryAsync(sessionId, cancellationToken);
    }

    public async Task<ChatHistoryResponse> StartNewSessionAsync(
        ArcActor actor,
        CancellationToken cancellationToken)
    {
        var sessionId = await CreateSessionAsync(actor, cancellationToken);
        return new ChatHistoryResponse(sessionId, []);
    }

    public async Task<(bool Saved, string? Error)> PersistTurnAsync(
        string sessionId,
        string userMessage,
        ChatMessageResponse response,
        ArcActor actor,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SetActiveSessionIdAsync(actor.Upn, sessionId, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            await _repository.AppendTurnAsync(
                new ChatSessionMessage(
                    Id: ChatMessageId(sessionId, "user"),
                    SessionId: sessionId,
                    Role: "user",
                    Content: userMessage,
                    Agent: null,
                    ReplyFormat: null,
                    CreatedUtc: now,
                    UserUpn: actor.Upn),
                new ChatSessionMessage(
                    Id: ChatMessageId(sessionId, "assistant"),
                    SessionId: sessionId,
                    Role: "assistant",
                    Content: response.Reply,
                    Agent: response.Agent,
                    ReplyFormat: response.ReplyFormat,
                    CreatedUtc: response.Timestamp,
                    UserUpn: actor.Upn),
                cancellationToken);

            _logger.LogInformation("Saved chat turn for session {SessionId}.", sessionId);
            return (true, null);
        }
        catch (Exception ex)
        {
            var hint = DescribeSaveFailure(ex);
            _logger.LogError(ex, "Chat history was not saved for session {SessionId}. {Hint}", sessionId, hint);
            return (false, hint);
        }
    }

    public async Task<ChatHistoryResponse> GetHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var messages = await _repository.ListBySessionAsync(sessionId, cancellationToken);
        return new ChatHistoryResponse(
            sessionId,
            messages.Select(m => new ChatHistoryMessage(
                m.Role,
                m.Content,
                m.Agent,
                m.ReplyFormat,
                m.CreatedUtc)).ToList());
    }

    private async Task<string> CreateSessionAsync(ArcActor actor, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        await _repository.SetActiveSessionIdAsync(actor.Upn, sessionId, cancellationToken);
        _logger.LogInformation("Created chat session {SessionId} for {UserUpn}.", sessionId, actor.Upn);
        return sessionId;
    }

    private static string ChatMessageId(string sessionId, string role) =>
        $"chat-{sessionId}-{role}-{Guid.NewGuid():N}";

    private static string DescribeSaveFailure(Exception ex)
    {
        var cosmos = FindCosmosException(ex);
        if (cosmos?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return "Cosmos DB rejected the account key (401). In Azure Portal open arc-vector-store → Keys, copy the primary key, and update ArcData:Cosmos:ConnectionString in appsettings.Development.local.json, then restart ARC.Api.";
        }

        if (cosmos?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return "Cosmos database or container was not found. Check ArcData:Cosmos:DatabaseId and DocumentsContainer in appsettings.Development.local.json.";
        }

        if (ex is DataAccessException dataEx)
            return dataEx.Message;

        return ex.Message;
    }

    private static CosmosException? FindCosmosException(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is CosmosException cosmos)
                return cosmos;
        }

        return null;
    }
}
