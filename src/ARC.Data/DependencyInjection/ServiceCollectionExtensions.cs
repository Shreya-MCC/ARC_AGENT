using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ARC.Data.Blob;
using ARC.Data.Configuration;
using ARC.Data.Cosmos;
using ARC.Data.Messaging;
using ARC.Data.Sql;

namespace ARC.Data.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArcData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ArcDataOptions>(configuration.GetSection(ArcDataOptions.SectionName));

        var vaultUri = ArcKeyVaultConfiguration.TryGetVaultUri(configuration);
        if (vaultUri is not null)
        {
            services.AddSingleton(_ => new SecretClient(vaultUri, new DefaultAzureCredential()));
            services.AddSingleton<IArcSecretStore, ArcSecretStore>();
        }

        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IDealerRepository, DealerRepository>();
        services.AddSingleton<IDepotMasterRepository, DepotMasterRepository>();
        services.AddSingleton<ILedgerRepository, LedgerRepository>();
        services.AddSingleton<IChequeRepository, ChequeRepository>();
        services.AddSingleton<IGateDecisionRepository, GateDecisionRepository>();
        services.AddSingleton<ILegalCaseRepository, LegalCaseRepository>();
        services.AddSingleton<IRecoveryCaseRepository, RecoveryCaseRepository>();

        services.AddSingleton<ICosmosClientFactory, CosmosClientFactory>();
        services.AddSingleton<IWorkflowStateRepository, WorkflowStateRepository>();
        services.AddSingleton<IMafCheckpointDocumentStore, MafCheckpointDocumentStore>();
        services.AddSingleton<IConversationStateRepository, ConversationStateRepository>();
        services.AddSingleton<IChatSessionRepository, ChatSessionRepository>();
        services.AddSingleton<IAuditRepository, AuditRepository>();

        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddSingleton<IEvidenceDocumentRepository, EvidenceDocumentRepository>();

        services.AddSingleton<IServiceBusPublisher, ServiceBusPublisher>();

        return services;
    }
}
