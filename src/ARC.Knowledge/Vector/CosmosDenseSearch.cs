using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using ARC.Data.Cosmos;
using ARC.Knowledge.Exceptions;
using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Vector;

/// <summary>
/// Dense (vector) search against the Cosmos documents container.
/// Uses VectorDistance when the account supports it; otherwise cosine over fetched embeddings.
/// </summary>
public sealed class CosmosDenseSearch : IDenseSearch
{
    private readonly Container _documents;
    private readonly ILogger<CosmosDenseSearch> _logger;

    public CosmosDenseSearch(ICosmosClientFactory cosmos, ILogger<CosmosDenseSearch> logger)
    {
        _documents = cosmos.Documents;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EvidenceSource>> SearchAsync(
        string text,
        float[]? embedding,
        string? region,
        string? documentCategory,
        string? requiredVersion,
        int topK,
        CancellationToken cancellationToken)
    {
        if (embedding is null || embedding.Length == 0)
        {
            _logger.LogDebug("Dense search skipped; no query embedding was supplied. Query length {Length}.", text?.Length ?? 0);
            return [];
        }

        try
        {
            var vectorHits = await TryVectorDistanceAsync(
                embedding, region, documentCategory, requiredVersion, topK, cancellationToken);
            if (vectorHits is not null)
            {
                _logger.LogInformation("Cosmos dense VectorDistance returned {Count} documents", vectorHits.Count);
                return vectorHits;
            }

            var fallback = await CosineFallbackAsync(
                embedding, region, documentCategory, requiredVersion, topK, cancellationToken);
            _logger.LogInformation("Cosmos dense cosine fallback returned {Count} documents", fallback.Count);
            return fallback;
        }
        catch (Exception ex) when (ex is not RetrievalFailedException)
        {
            throw new RetrievalFailedException("Dense retrieval against Cosmos documents failed.", ex);
        }
    }

    private async Task<IReadOnlyList<EvidenceSource>?> TryVectorDistanceAsync(
        float[] embedding,
        string? region,
        string? documentCategory,
        string? requiredVersion,
        int topK,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT TOP @topK c.id, c.title, c.content, c.status, c.documentCategory, c.version,
                   c.regionScope, c.blobLocation, VectorDistance(c.embedding, @embedding) AS similarity
            FROM c
            WHERE IS_DEFINED(c.embedding)
              AND c.status = 'ACTIVE'
              AND (NOT IS_DEFINED(c.docType) OR (c.docType != 'chatMessage' AND c.docType != 'chatSessionPointer'))
              AND (@category = null OR c.documentCategory = @category)
              AND (@version = null OR c.version = @version)
              AND (@region = null OR NOT IS_DEFINED(c.regionScope) OR ARRAY_CONTAINS(c.regionScope, @region))
            ORDER BY VectorDistance(c.embedding, @embedding)
            """;

        try
        {
            var definition = new QueryDefinition(sql)
                .WithParameter("@topK", Math.Max(1, topK))
                .WithParameter("@category", documentCategory)
                .WithParameter("@version", requiredVersion)
                .WithParameter("@region", region)
                .WithParameter("@embedding", embedding);

            var results = new List<EvidenceSource>();
            using var iterator = _documents.GetItemQueryIterator<CosmosDenseHit>(definition);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                foreach (var hit in page)
                    results.Add(EvidenceMapping.ToSource(hit.ToDocument(), hit.Similarity, "cosmos-dense"));
            }

            return results;
        }
        catch (CosmosException ex)
        {
            _logger.LogWarning(ex, "Cosmos VectorDistance is unavailable; using in-process cosine.");
            return null;
        }
    }

    private async Task<IReadOnlyList<EvidenceSource>> CosineFallbackAsync(
        float[] embedding,
        string? region,
        string? documentCategory,
        string? requiredVersion,
        int topK,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 200 c.id, c.title, c.content, c.status, c.documentCategory, c.version,
                   c.regionScope, c.blobLocation, c.embedding
            FROM c
            WHERE IS_DEFINED(c.embedding)
              AND c.status = 'ACTIVE'
              AND (NOT IS_DEFINED(c.docType) OR (c.docType != 'chatMessage' AND c.docType != 'chatSessionPointer'))
              AND (@category = null OR c.documentCategory = @category)
              AND (@version = null OR c.version = @version)
              AND (@region = null OR NOT IS_DEFINED(c.regionScope) OR ARRAY_CONTAINS(c.regionScope, @region))
            """;

        var definition = new QueryDefinition(sql)
            .WithParameter("@category", documentCategory)
            .WithParameter("@version", requiredVersion)
            .WithParameter("@region", region);

        var scored = new List<EvidenceSource>();
        using var iterator = _documents.GetItemQueryIterator<IndexedDocument>(definition);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                if (doc.Embedding is null || doc.Embedding.Length == 0)
                    continue;
                var score = EvidenceMapping.Cosine(embedding, doc.Embedding);
                scored.Add(EvidenceMapping.ToSource(doc, score, "cosmos-dense"));
            }
        }

        return scored
            .OrderByDescending(s => s.Score ?? 0)
            .Take(Math.Max(1, topK))
            .ToList();
    }

    private sealed record CosmosDenseHit(
        string Id,
        string Title,
        string Content,
        string Status,
        string? DocumentCategory,
        string? Version,
        string? RegionScope,
        string? BlobLocation,
        double Similarity)
    {
        public IndexedDocument ToDocument() =>
            new(Id, Title, Content, Status, DocumentCategory, Version, RegionScope, BlobLocation, null);
    }
}
