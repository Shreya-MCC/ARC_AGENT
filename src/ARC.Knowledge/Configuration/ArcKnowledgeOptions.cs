namespace ARC.Knowledge.Configuration;

public sealed class ArcKnowledgeOptions
{
    public const string SectionName = "ArcKnowledge";

    /// <summary>Document Intelligence endpoint. Key is never stored here — use managed identity.</summary>
    public string DocumentIntelligenceEndpoint { get; set; } = "";

    /// <summary>Azure OpenAI / Foundry endpoint for query embeddings (e.g. text-embedding-3-small).</summary>
    public string EmbeddingEndpoint { get; set; } = "";

    public string EmbeddingDeployment { get; set; } = "";

    /// <summary>API key for local dev. In Azure, leave empty and use managed identity.</summary>
    public string EmbeddingApiKey { get; set; } = "";

    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>Model ids are environment-specific. Defaults are extraction models, not legal rules.</summary>
    public string ChequeModelId { get; set; } = "prebuilt-check.us";

    public string LayoutModelId { get; set; } = "prebuilt-layout";

    public int RetrievalTopK { get; set; } = 8;

    /// <summary>On-disk Lucene BM25 index directory (lexical / word search).</summary>
    public string LexicalIndexDirectory { get; set; } = Path.Combine("data", "lucene-index");

    public int DenseCandidates { get; set; } = 24;

    public int LexicalCandidates { get; set; } = 24;

    public int FusionCandidates { get; set; } = 16;

    /// <summary>RRF constant k (Cormack). Higher k reduces the gap between ranks.</summary>
    public int RrfK { get; set; } = 60;

    /// <summary>Source auto-accept thresholds for extraction quality only — not eligibility.</summary>
    public decimal ChequeNumberConfidence { get; set; } = 0.90m;
    public decimal MicrConfidence { get; set; } = 0.90m;
    public decimal AmountConfidence { get; set; } = 0.85m;
}
