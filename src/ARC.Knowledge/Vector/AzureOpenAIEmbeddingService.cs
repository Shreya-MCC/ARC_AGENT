using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ARC.Knowledge.Configuration;
using ARC.Knowledge.Exceptions;
using OpenAI.Embeddings;

namespace ARC.Knowledge.Vector;

/// <summary>Azure OpenAI / Foundry embeddings for dense vector search.</summary>
public sealed class AzureOpenAIEmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<AzureOpenAIEmbeddingService> _logger;

    public AzureOpenAIEmbeddingService(
        IOptions<ArcKnowledgeOptions> options,
        ILogger<AzureOpenAIEmbeddingService> logger)
    {
        var knowledge = options.Value;
        if (string.IsNullOrWhiteSpace(knowledge.EmbeddingEndpoint)
            || string.IsNullOrWhiteSpace(knowledge.EmbeddingDeployment))
        {
            throw new KnowledgeException(
                "ArcKnowledge:EmbeddingEndpoint and ArcKnowledge:EmbeddingDeployment are required for embeddings.");
        }

        _logger = logger;
        var endpoint = new Uri(knowledge.EmbeddingEndpoint.Trim());
        var deployment = knowledge.EmbeddingDeployment.Trim();

        AzureOpenAIClient azure = !string.IsNullOrWhiteSpace(knowledge.EmbeddingApiKey)
            ? new AzureOpenAIClient(endpoint, new AzureKeyCredential(knowledge.EmbeddingApiKey))
            : knowledge.UseManagedIdentity
                ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
                : throw new KnowledgeException(
                    "Configure ArcKnowledge:EmbeddingApiKey for local dev, or set UseManagedIdentity=true.");

        _client = azure.GetEmbeddingClient(deployment);
    }

    public bool IsConfigured => true;

    public async Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var response = await _client.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
            return response.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding generation failed for text length {Length}.", text.Length);
            throw new RetrievalFailedException("Embedding generation failed.", ex);
        }
    }
}
