using Kynakee.Modules.KnowledgeBase.Infrastructure.Persistence;
using Kynakee.Modules.KnowledgeBase.Domain.Repositories;
using Kynakee.Modules.KnowledgeBase.Infrastructure.Repositories;
using Kynakee.Modules.KnowledgeBase.Application.Abstractions;
using Kynakee.Modules.KnowledgeBase.Infrastructure.VectorSearch;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace Kynakee.Modules.KnowledgeBase
{
    public static class KnowledgeBaseModuleDependencyExtensions
    {
        public static IServiceCollection AddKnowledgeBaseModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddDbContext<KnowledgeBaseDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"));
            });

            services.AddScoped<IModuleTransactionParticipant,
                KnowledgeBaseTransactionParticipant>();
            services.AddScoped<ICanonicalConceptRepository,
                EfCanonicalConceptRepository>();
            services.AddScoped<IAPUTemplateRepository,
                EfAPUTemplateRepository>();
            services.AddOptions<KnowledgeBaseVectorOptions>()
                .Bind(configuration.GetSection(KnowledgeBaseVectorOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Qdrant host is required.")
                .Validate(options => options.Port is > 0 and <= 65535, "Qdrant port is invalid.")
                .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.CanonicalConceptsCollection) &&
                    !string.IsNullOrWhiteSpace(options.ApuStructuresCollection) &&
                    !string.IsNullOrWhiteSpace(options.ProjectContextsCollection),
                    "Qdrant collection names are required.")
                .ValidateOnStart();
            services.AddSingleton(serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<KnowledgeBaseVectorOptions>>()
                    .Value;
                return new QdrantClient(
                    options.Host,
                    options.Port,
                    apiKey: options.ApiKey);
            });
            services.AddSingleton<IKnowledgeBaseVectorIndex,
                QdrantKnowledgeBaseVectorIndex>();

            return services;
        }
    }
}
