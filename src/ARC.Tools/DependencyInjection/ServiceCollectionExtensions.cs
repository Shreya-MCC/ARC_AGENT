using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ARC.Domain.Limitation;
using ARC.Domain.Rules;
using ARC.Tools.Depot;
using ARC.Tools.Drafting;
using ARC.Tools.Evidence;
using ARC.Tools.Field;
using ARC.Tools.Insights;
using ARC.Tools.Knowledge;
using ARC.Tools.Legal;
using ARC.Tools.Models;
using ARC.Tools.Notice;
using ARC.Tools.Reconciliation;
using ARC.Tools.Risk;

namespace ARC.Tools.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArcTools(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ArcToolsOptions>(configuration.GetSection(ArcToolsOptions.SectionName));

        var rules = RuleConfiguration.SourceIllustrative();
        services.AddSingleton(rules);
        services.AddSingleton(_ => RuleEngine.CreateDefault(rules));
        services.AddSingleton<ILimitationClockService, LimitationClockService>();

        services.AddSingleton<DepotMasterTool>();
        services.AddSingleton<ReconciliationTool>();
        services.AddSingleton<RiskPrioritisationTool>();
        services.AddSingleton<NoticeDecisionTool>();
        services.AddSingleton<LegalEligibilityTool>();
        services.AddSingleton<DraftingVerificationTool>();
        services.AddSingleton<FieldOrchestrationTool>();
        services.AddSingleton<EvidenceCaseFileTool>();
        services.AddSingleton<KnowledgeRetrievalTool>();
        services.AddSingleton<SupervisoryInsightTool>();

        return services;
    }
}
