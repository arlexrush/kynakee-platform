using Kynakee.Modules.Ai.Infrastructure.Persistence;
using Kynakee.Modules.Ai.Infrastructure.AI;
using Kynakee.Modules.AI.Contracts;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Ai
{
    public static class AiModuleDependencyExtensions
    {
        public static IServiceCollection AddAiModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();
            services.AddOptions<AiOptions>()
                .Bind(configuration.GetSection(AiOptions.SectionName))
                .ValidateOnStart();

            services.AddHttpClient<IAiProviderClient, AiProviderClient>(client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            });
            services.AddSingleton<AiModelRegistry>();
            services.AddScoped<AiMediaContentResolver>();
            services.AddScoped<IKynakeeAgentService, KynakeeAgentService>();

            services.AddDbContext<AiDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"));
            });

            services.AddScoped<IModuleTransactionParticipant,
                AiTransactionParticipant>();

            return services;
        }
    }
}
