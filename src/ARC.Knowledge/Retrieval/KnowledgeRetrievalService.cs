using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ARC.Domain.ValueObjects;
using ARC.Knowledge.Configuration;
using ARC.Knowledge.Graph;
using ARC.Knowledge.Vector;

namespace ARC.Knowledge.Retrieval;

public interface IKnowledgeRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Hybrid retrieval: graph traversal + Cosmos dense + Lucene BM25, fused with RRF.
/// Does not decide eligibility, notices, or legal progression.
/// </summary>
public sealed class KnowledgeRetrievalService : IKnowledgeRetrievalService
{
    private readonly IGraphTraversal _graph;
    private readonly IVectorSearch _search;
    private readonly IEmbeddingService _embeddings;
    private readonly ArcKnowledgeOptions _options;
    private readonly ILogger<KnowledgeRetrievalService> _logger;

    public KnowledgeRetrievalService(
        IGraphTraversal graph,
        IVectorSearch search,
        IEmbeddingService embeddings,
        IOptions<ArcKnowledgeOptions> options,
        ILogger<KnowledgeRetrievalService> logger)
    {
        _graph = graph;
        _search = search;
        _embeddings = embeddings;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken)
    {
        var topK = query.TopK > 0 ? query.TopK : _options.RetrievalTopK;
        IReadOnlyList<GraphNode> nodes = [];
        if (!string.IsNullOrWhiteSpace(query.DealerUrn))
            nodes = await _graph.TraverseDealerAsync(new DealerUrn(query.DealerUrn), cancellationToken);

        var embedding = query.Embedding;
        if (embedding is null && _embeddings.IsConfigured && !string.IsNullOrWhiteSpace(query.Text))
            embedding = await _embeddings.EmbedAsync(query.Text, cancellationToken);

        var sources = await _search.SearchAsync(
            query.Text,
            embedding,
            query.Region,
            query.DocumentCategory,
            query.RequiredVersion,
            topK,
            cancellationToken);

        _logger.LogInformation(
            "Retrieval complete correlation {CorrelationId} sources {SourceCount} graph {GraphCount}",
            query.CorrelationId,
            sources.Count,
            nodes.Count);

        return new RetrievalResult(sources, nodes, DateTimeOffset.UtcNow);
    }
}
