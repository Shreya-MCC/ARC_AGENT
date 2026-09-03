namespace ARC.Data.Configuration;

public sealed class ArcDataOptions
{
    public const string SectionName = "ArcData";

    public SqlStoreOptions Sql { get; set; } = new();
    public CosmosStoreOptions Cosmos { get; set; } = new();
    public BlobStoreOptions Blob { get; set; } = new();
    public ServiceBusOptions ServiceBus { get; set; } = new();
}

public sealed class SqlStoreOptions
{
    /// <summary>SQL connection string. Do not put passwords in source; use Key Vault / user secrets.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>When true, DefaultAzureCredential is used (managed identity in Azure).</summary>
    public bool UseManagedIdentity { get; set; }
}

public sealed class CosmosStoreOptions
{
    public string AccountEndpoint { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public bool UseManagedIdentity { get; set; } = true;
    public string DatabaseId { get; set; } = "arc";
    public string CheckpointsContainer { get; set; } = "checkpoints";
    public string CycleStateContainer { get; set; } = "cycleState";
    public string AuditContainer { get; set; } = "auditEvents";
    public string ConversationContainer { get; set; } = "conversationState";
    public string DocumentsContainer { get; set; } = "documents";

    /// <summary>
    /// Partition key mode for chat history in knowledgeChunks:
    /// sessionId (/sessionId) or documentCategory (/documentCategory).
    /// Chat turns are stored with documentCategory = chat-session and sessionId = the chat session.
    /// </summary>
    public string ChatSessionPartitionKey { get; set; } = "documentCategory";
}

public sealed class BlobStoreOptions
{
    public string ServiceUri { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public bool UseManagedIdentity { get; set; } = true;
    public string EvidenceContainer { get; set; } = "evidence";
    public string LegalContainer { get; set; } = "legal-worm";
}

public sealed class ServiceBusOptions
{
    public string FullyQualifiedNamespace { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public bool UseManagedIdentity { get; set; } = true;
    public string CycleFanOutQueue { get; set; } = "arc-cycle-fanout";
    public string AlertQueue { get; set; } = "arc-alerts";
    public string GateNotificationQueue { get; set; } = "arc-gate-notifications";
    public string GateResumeQueue { get; set; } = "arc-gate-resume";
}
