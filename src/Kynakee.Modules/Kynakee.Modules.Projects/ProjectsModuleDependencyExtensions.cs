using FluentValidation;
using Kynakee.Modules.Projects.Domain.Repositories;
using Kynakee.Modules.Projects.Infrastructure.Persistence;
using Kynakee.Modules.Projects.Infrastructure.Repositories;
using Kynakee.Modules.Projects.Infrastructure.Storage;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Amazon.Runtime;
using Amazon.S3;

namespace Kynakee.Modules.Projects
{
    public static class ProjectsModuleDependencyExtensions
    {
        public static IServiceCollection AddProjectsModule(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddSingleton<IValidateOptions<ObjectStorageOptions>, ObjectStorageOptionsValidator>();
            services.AddOptions<ObjectStorageOptions>()
                .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
                .ValidateOnStart();

            services.AddSingleton<IAmazonS3>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
                if (!options.Enabled || !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
                {
                    throw new InvalidOperationException(ObjectStorageMessages.Get("ConfigurationRequired"));
                }
                return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), new AmazonS3Config
                {
                    ServiceURL = endpoint.GetLeftPart(UriPartial.Authority),
                    AuthenticationRegion = options.Region,
                    ForcePathStyle = true,
                    Timeout = TimeSpan.FromSeconds(30),
                    MaxErrorRetry = 2
                });
            });

            services.AddScoped<ITrustedMediaContentSource, S3TrustedMediaContentSource>();

            services.AddDbContext<ProjectsDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"));
            });

            services.AddScoped<IModuleTransactionParticipant, ProjectsTransactionParticipant>(); // Register the transaction participant for the Projects module
            services.AddScoped<IProjectRepository, EfProjectRepository>(); // Register EfProjectRepository as the implementation for IProjectRepository

            return services;
        }
    }
}
