using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ARC.Knowledge.Configuration;
using ARC.Knowledge.Documents;
using ARC.Knowledge.Fusion;
using ARC.Knowledge.Graph;
using ARC.Knowledge.Lexical;
using ARC.Knowledge.Retrieval;
using ARC.Knowledge.Vector;

namespace ARC.Knowledge.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArcKnowledge(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ArcKnowledgeOptions>(configuration.GetSection(ArcKnowledgeOptions.SectionName));
        services.AddSingleton<IEmbeddingService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ArcKnowledgeOptions>>().Value;
            return !string.IsNullOrWhiteSpace(options.EmbeddingEndpoint)
                   && !string.IsNullOrWhiteSpace(options.EmbeddingDeployment)
                ? ActivatorUtilities.CreateInstance<AzureOpenAIEmbeddingService>(sp)
                : new NullEmbeddingService();
        });
        services.AddSingleton<IDocumentIntelligenceService, DocumentIntelligenceService>();
        services.AddSingleton<IGraphTraversal, GraphTraversal>();
        services.AddSingleton<IDenseSearch, CosmosDenseSearch>();
        services.AddSingleton<ILexicalCorpus, CosmosLexicalCorpus>();
        services.AddSingleton<LuceneLexicalIndex>();
        services.AddSingleton<ILexicalSearch, LuceneLexicalSearch>();
        services.AddSingleton<IRankFusion>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ArcKnowledgeOptions>>().Value;
            return new ReciprocalRankFusion(options.RrfK);
        });
        services.AddSingleton<IVectorSearch, HybridDocumentSearch>();
        services.AddSingleton<IKnowledgeRetrievalService, KnowledgeRetrievalService>();
        return services;
    }
}
