using FluentValidation;
using Kynakee.Api.Endpoints;
using Kynakee.Api.Application.Abstractions;
using Kynakee.Api.Application.Behaviors;
using Kynakee.Api.Infrastructure.Persistence;
using Kynakee.Modules.Ai;
using Kynakee.Modules.Billing;
using Kynakee.Modules.Bots;
using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.KnowledgeBase;
using Kynakee.Modules.Mcp;
using Kynakee.Modules.Mcp.Application.Commands.RegisterMcpProvider;
using Kynakee.Modules.Projects;
using Kynakee.Modules.Projects.Domain.Repositories;
using Kynakee.Modules.Projects.Infrastructure.Repositories;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Diagnostics.CodeAnalysis;
using System.Threading.RateLimiting;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Kynakee.Api
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    [SuppressMessage("Design", "CA1515", Justification = "This extension is called by the API host composition root.")]
    public static class KynakeeApiDependenceExtensions
    {
        public static IServiceCollection AddApiDependencies(this IServiceCollection services, IConfiguration configuration)
        {           
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            services.AddProjectsModule(configuration);
            services.AddScoped<IProjectRepository, EfProjectRepository>();

            services.AddBillingModule(configuration);
            services.AddIdentityModule(configuration);
            var signingKey = configuration["Authentication:SigningKey"];
            if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            {
                throw new InvalidOperationException(
                    "Authentication:SigningKey must be configured with at least 32 UTF-8 bytes.");
            }

            var issuer = configuration["Authentication:Issuer"] ?? "Kynakee";
            var audience = configuration["Authentication:Audience"] ?? "KynakeeClients";
            var signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                });
            services.AddAuthorization();
            services.AddRateLimiter(options =>
            {
                options.AddPolicy("identity-auth", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });
            services.AddKnowledgeBaseModule(configuration);
            services.AddMcpModule(configuration);
            services.AddAiModule(configuration);
            services.AddBotsModule(configuration);

            services.AddScoped<ICorrelationContext, CorrelationContext>();            
            services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblies(
                    typeof(KynakeeApiDependenceExtensions).Assembly,
                    typeof(RegisterTenantOwnerCommandHandler).Assembly,
                    typeof(RegisterMcpProviderCommandHandler).Assembly);
            });

            services.AddValidatorsFromAssembly(typeof(KynakeeApiDependenceExtensions).Assembly);


            services.AddHttpContextAccessor();

            services.AddScoped<HttpTenantContext>();

            services.AddScoped<ITenantContext>(serviceProvider => serviceProvider.GetRequiredService<HttpTenantContext>());

            services.AddScoped<ITenantContextWriter>(serviceProvider => serviceProvider.GetRequiredService<HttpTenantContext>());

            

            services.AddScoped<ITransactionManager, ModuleTransactionManager>(); // Register the ModuleTransactionManager as the implementation of ITransactionManager
            services.AddScoped<IDomainEventCollector, ModuleDomainEventCollector>();  // Register the ModuleDomainEventCollector as the implementation of IDomainEventCollector
            services.AddScoped<IPostCommitActionDispatcher, PostCommitActionDispatcher>(); // Register the PostCommitActionDispatcher as the implementation of IPostCommitActionDispatcher




            // Register pipeline behaviors for MediatR

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantIsolationBehavior<,>));

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TokenGateBehavior<,>));

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainEventDispatchBehavior<,>));

            //services.AddScoped<ITokenGateService, BillingTokenGateService>(); // Register the BillingTokenGateService as the implementation of ITokenGateService

            return services;
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
