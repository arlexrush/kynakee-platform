using Kynakee.Modules.Projects.Infrastructure.Persistence;
using Kynakee.Modules.Projects.Infrastructure.Storage;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace Kynakee.Modules.Projects
{
    public static class ProjectsModuleDependencyExtensions
    {
        public static IServiceCollection AddProjectsModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddSingleton<IValidateOptions<MinioOptions>, MinioOptionsValidator>();
            services.AddOptions<MinioOptions>()
                .Bind(configuration.GetSection(MinioOptions.SectionName))
                .ValidateOnStart();

            services.AddSingleton<IMinioClient>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<MinioOptions>>().Value;
                if (!options.Enabled || !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
                {
                    throw new InvalidOperationException("MinIO must be configured before its client is resolved.");
                }

                var client = new MinioClient()
                    .WithEndpoint(endpoint.Authority)
                    .WithCredentials(options.AccessKey, options.SecretKey);
                return options.UseSsl ? client.WithSSL().Build() : client.Build();
            });
            services.AddScoped<ITrustedMediaContentSource, MinioTrustedMediaContentSource>();

            services.AddDbContext<ProjectsDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"));
            });

            services.AddScoped<IModuleTransactionParticipant,
                ProjectsTransactionParticipant>();

            return services;
        }
    }
}
