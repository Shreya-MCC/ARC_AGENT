using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ARC.Data.Configuration;
using ARC.Data.Exceptions;

namespace ARC.Data.Cosmos;

public interface IChatSessionRepository
{
    Task AppendTurnAsync(ChatSessionMessage user, ChatSessionMessage assistant, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatSessionMessage>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken);
    Task<string?> GetActiveSessionIdAsync(string userUpn, CancellationToken cancellationToken);
    Task SetActiveSessionIdAsync(string userUpn, string sessionId, CancellationToken cancellationToken);
}

public sealed record ChatSessionMessage(
    string Id,
    string SessionId,
    string Role,
    string Content,
    string? Agent,
    string? ReplyFormat,
    DateTimeOffset CreatedUtc,
    string? UserUpn);

public sealed class ChatSessionRepository : IChatSessionRepository
{
    public const string ChatDocumentCategory = "chat-session";
    public const string ChatDocType = "chatMessage";
    public const string SessionPointerDocType = "chatSessionPointer";
    public const string ChatStatus = "CHAT";

    private readonly Container _container;
    private readonly CosmosStoreOptions _options;
    private readonly ILogger<ChatSessionRepository> _logger;

    public ChatSessionRepository(
        ICosmosClientFactory cosmos,
        IOptions<ArcDataOptions> options,
        ILogger<ChatSessionRepository> logger)
    {
        _container = cosmos.Documents;
        _options = options.Value.Cosmos;
        _logger = logger;
    }

    public async Task AppendTurnAsync(
        ChatSessionMessage user,
        ChatSessionMessage assistant,
        CancellationToken cancellationToken)
    {
        var keys = CandidatePartitionKeys(user.SessionId);
        Exception? lastError = null;

        foreach (var partitionKey in keys)
        {
            try
            {
                await UpsertAsync(ToDocument(user), partitionKey, cancellationToken);
                await UpsertAsync(ToDocument(assistant), partitionKey, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not DataAccessException)
            {
                lastError = ex;
                if (!ShouldTryNextPartition(StatusOf(ex), ex.Message))
                    break;
            }
        }

        _logger.LogError(
            lastError,
            "Cosmos upsert failed for chat session {SessionId} in {Database}/{Container} using partition mode {PartitionMode}.",
            user.SessionId,
            _options.DatabaseId,
            _options.DocumentsContainer,
            _options.ChatSessionPartitionKey);

        throw new DataAccessException(
            $"Failed to save chat session message. {Describe(lastError)} "
            + $"Check ArcData:Cosmos:ChatSessionPartitionKey matches the knowledgeChunks partition key "
            + $"(use 'sessionId' for /sessionId, or 'documentCategory' for /documentCategory).",
            lastError);
    }

    public async Task<IReadOnlyList<ChatSessionMessage>> ListBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.id, c.sessionId, c.role, c.content, c.agent, c.replyFormat, c.createdUtc, c.userUpn
            FROM c
            WHERE c.docType = 'chatMessage' AND c.sessionId = @sessionId
            ORDER BY c.createdUtc ASC
            """;

        var definition = new QueryDefinition(sql).WithParameter("@sessionId", sessionId);
        var attempts = CandidatePartitionKeys(sessionId)
            .Select(pk => new QueryRequestOptions { PartitionKey = pk })
            .Append(new QueryRequestOptions())
            .ToList();

        Exception? lastError = null;
        for (var i = 0; i < attempts.Count; i++)
        {
            try
            {
                var messages = new List<ChatSessionMessage>();
                using var iterator = _container.GetItemQueryIterator<ChatSessionDocument>(
                    definition,
                    requestOptions: attempts[i]);

                while (iterator.HasMoreResults)
                {
                    var page = await iterator.ReadNextAsync(cancellationToken);
                    foreach (var doc in page)
                        messages.Add(doc.ToMessage());
                }

                if (messages.Count > 0 || i == attempts.Count - 1)
                    return messages;
            }
            catch (Exception ex) when (ex is not DataAccessException)
            {
                lastError = ex;
                if (!ShouldTryNextPartition(StatusOf(ex), ex.Message) || i == attempts.Count - 1)
                    break;
            }
        }

        throw new DataAccessException($"Failed to load chat session messages. {Describe(lastError)}", lastError);
    }

    public async Task<string?> GetActiveSessionIdAsync(string userUpn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 1 c.activeSessionId
            FROM c
            WHERE c.docType = 'chatSessionPointer' AND c.userUpn = @userUpn
            ORDER BY c.updatedUtc DESC
            """;

        var definition = new QueryDefinition(sql).WithParameter("@userUpn", userUpn);
        using var iterator = _container.GetItemQueryIterator<SessionPointerDocument>(definition);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                if (!string.IsNullOrWhiteSpace(doc.activeSessionId))
                    return doc.activeSessionId;
            }
        }

        return null;
    }

    public async Task SetActiveSessionIdAsync(
        string userUpn,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var document = new SessionPointerDocument
        {
            id = PointerId(userUpn),
            documentCategory = ChatDocumentCategory,
            docType = SessionPointerDocType,
            status = ChatStatus,
            userUpn = userUpn,
            activeSessionId = sessionId,
            updatedUtc = DateTimeOffset.UtcNow
        };

        Exception? lastError = null;
        foreach (var partitionKey in CandidatePartitionKeys(sessionId))
        {
            try
            {
                await _container.UpsertItemAsync(document, partitionKey, cancellationToken: cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not DataAccessException)
            {
                lastError = ex;
                if (!ShouldTryNextPartition(StatusOf(ex), ex.Message))
                    break;
            }
        }

        throw new DataAccessException($"Failed to save active chat session pointer. {Describe(lastError)}", lastError);
    }

    private static string PointerId(string userUpn) =>
        $"session-pointer-{userUpn.Trim().ToLowerInvariant().Replace('@', '-').Replace('.', '-')}";

    private async Task UpsertAsync(
        ChatSessionDocument document,
        PartitionKey partitionKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await _container.UpsertItemAsync(document, partitionKey, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw;
        }
    }

    private IReadOnlyList<PartitionKey> CandidatePartitionKeys(string sessionId)
    {
        var sessionPk = new PartitionKey(sessionId);
        var categoryPk = new PartitionKey(ChatDocumentCategory);
        return UsesDocumentCategoryPartition()
            ? [categoryPk, sessionPk]
            : [sessionPk, categoryPk];
    }

    private bool UsesDocumentCategoryPartition() =>
        _options.ChatSessionPartitionKey.Equals("documentCategory", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldTryNextPartition(HttpStatusCode? status, string? message)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            return false;

        var text = message ?? "";
        return status == HttpStatusCode.BadRequest
            || text.Contains("partition", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cross partition", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpStatusCode? StatusOf(Exception ex) =>
        ex is CosmosException cosmos ? cosmos.StatusCode : null;

    private static ChatSessionDocument ToDocument(ChatSessionMessage message) =>
        new()
        {
            id = message.Id,
            documentCategory = ChatDocumentCategory,
            docType = ChatDocType,
            status = ChatStatus,
            sessionId = message.SessionId,
            role = message.Role,
            content = message.Content,
            title = TitleOf(message),
            agent = message.Agent,
            replyFormat = message.ReplyFormat,
            createdUtc = message.CreatedUtc,
            userUpn = message.UserUpn
        };

    private static string TitleOf(ChatSessionMessage message)
    {
        var prefix = message.Role == "user" ? "User" : "Assistant";
        var text = message.Content.Replace('\n', ' ').Trim();
        if (text.Length > 80)
            text = text[..80] + "…";
        return $"{prefix}: {text}";
    }

    private static string Describe(Exception? ex) =>
        ex is null ? "Unknown Cosmos error."
        : ex.InnerException is null ? ex.Message
        : $"{ex.Message} ({ex.InnerException.Message})";

    private sealed class ChatSessionDocument
    {
        public string id { get; set; } = "";
        public string documentCategory { get; set; } = ChatDocumentCategory;
        public string docType { get; set; } = ChatDocType;
        public string status { get; set; } = ChatStatus;
        public string sessionId { get; set; } = "";
        public string role { get; set; } = "";
        public string content { get; set; } = "";
        public string title { get; set; } = "";
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? agent { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? replyFormat { get; set; }
        public DateTimeOffset createdUtc { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? userUpn { get; set; }

        public ChatSessionMessage ToMessage() =>
            new(id, sessionId, role, content, agent, replyFormat, createdUtc, userUpn);
    }

    private sealed class SessionPointerDocument
    {
        public string id { get; set; } = "";
        public string documentCategory { get; set; } = ChatDocumentCategory;
        public string docType { get; set; } = SessionPointerDocType;
        public string status { get; set; } = ChatStatus;
        public string userUpn { get; set; } = "";
        public string activeSessionId { get; set; } = "";
        public DateTimeOffset updatedUtc { get; set; }
    }
}
