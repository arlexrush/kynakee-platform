using Kynakee.Api.Application.Abstractions;
using Kynakee.Api.Application.Behaviors;
using Kynakee.Api.Infrastructure.ExceptionHandling;
using Kynakee.Api.Infrastructure.Identity;
using Kynakee.Api.Infrastructure.Persistence;
using Kynakee.Modules.Ai;
using Kynakee.Modules.Billing;
using Kynakee.Modules.Bots;
using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Kynakee.Modules.KnowledgeBase;
using Kynakee.Modules.Mcp;
using Kynakee.Modules.Mcp.Application.Commands.RegisterMcpProvider;
using Kynakee.Modules.Projects;
using Kynakee.Modules.Projects.Domain.Repositories;
using Kynakee.Modules.Projects.Infrastructure.Repositories;
using Kynakee.Modules.SharedKernel.Contracts;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.RateLimiting;

namespace Kynakee.Api
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    [SuppressMessage("Design", "CA1515", Justification = "This extension is called by the API host composition root.")]
    public static class KynakeeApiDependenceExtensions
    {
        public static IServiceCollection AddApiDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            services.AddProjectsModule(configuration);  // Register the Projects module dependencies         
            services.AddBillingModule(configuration); // Register the Billing module dependencies
            services.AddIdentityModule(configuration); // Register the Identity module dependencies
            services.AddKnowledgeBaseModule(configuration); // Register the KnowledgeBase module dependencies
            services.AddMcpModule(configuration); // Register the Mcp module dependencies
            services.AddAiModule(configuration); // Register the Ai module dependencies
            services.AddBotsModule(configuration); // Register the Bots module dependencies

            var signingKey = configuration["Authentication:SigningKey"]; // Retrieve the signing key from configuration
            if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            {
                throw new InvalidOperationException(
                    "Authentication:SigningKey must be configured with at least 32 UTF-8 bytes.");
            }

            var issuer = configuration["Authentication:Issuer"] ?? "Kynakee";
            var audience = configuration["Authentication:Audience"] ?? "KynakeeClients";
            var signingKeyBytes = Encoding.UTF8.GetBytes(signingKey); // Convert the signing key to bytes for use in token validation

            // Register the IdentityEntityTagProtector as a singleton service, This is used for protecting the Idempotency request: generate hashes from password and request, and cifrate the result for reproduce.
            services.AddSingleton<IHttpIdempotencyProtector>(new IdentityHttpIdempotencyProtector(configuration));
            services.AddHostedService<IdentityIdempotencyOutcomePurger>();
            services.AddSingleton(new InvitationEmailPayloadProtector(configuration));
            services.AddSingleton(new Lazy<MailOAuthSession>(() => new MailOAuthSession(configuration)));
            services.AddScoped<IInvitationEmailJobStore, InvitationEmailJobStore>();
            services.AddScoped<InvitationEmailJobProcessor>();
            services.AddSingleton<AccountEmailPayloadProtector>();
            services.AddScoped<IAccountEmailJobStore, AccountEmailJobStore>();
            services.AddScoped<AccountEmailJobProcessor>();
            services.AddHostedService<InvitationEmailWorker>();

            // Register the IdentityEntityTagProtector as a singleton service, This is used for create and validate entity tags (ETag) link to identifier and the resource version and request for detecting concurrent changes.
            services.AddSingleton(new IdentityEntityTagProtector(signingKey));

            // Configure JWT Bearer authentication with the specified parameters, including issuer, audience, signing key, and token validation settings.
            services.AddScoped<IdentityJwtBearerEvents>();
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
                    options.EventsType = typeof(IdentityJwtBearerEvents);
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
            

            services.AddScoped<ICorrelationContext, CorrelationContext>(); // Register the CorrelationContext as a scoped service for managing correlation IDs in requests
            services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>(); // Register the MediatRDomainEventDispatcher as the implementation of IDomainEventDispatcher for dispatching domain events using MediatR

            // Register MediatR and configure it to scan for handlers in the specified assemblies, including the current assembly and the assemblies containing command handlers for tenant owner registration and MCP provider registration.
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblies(typeof(KynakeeApiDependenceExtensions).Assembly));

            // Register FluentValidation validators from the current assembly, allowing automatic validation of requests using the registered validators.
            services.AddValidatorsFromAssembly(typeof(KynakeeApiDependenceExtensions).Assembly);


            services.AddHttpContextAccessor();

            services.AddScoped<Application.Abstractions.HttpTenantContext>();

            services.AddScoped<ITenantContext>(serviceProvider => serviceProvider.GetRequiredService<Application.Abstractions.HttpTenantContext>());

            services.AddScoped<ITenantContextWriter>(serviceProvider => serviceProvider.GetRequiredService<Application.Abstractions.HttpTenantContext>());



            services.AddScoped<ITransactionManager, ModuleTransactionManager>(); // Register the ModuleTransactionManager as the implementation of ITransactionManager
            services.AddScoped<IDomainEventCollector, ModuleDomainEventCollector>();  // Register the ModuleDomainEventCollector as the implementation of IDomainEventCollector
            services.AddScoped<IPostCommitActionDispatcher, PostCommitActionDispatcher>(); // Register the PostCommitActionDispatcher as the implementation of IPostCommitActionDispatcher
            services.AddExceptionHandler<IdentityUniqueConstraintExceptionHandler>();
            services.AddProblemDetails();




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
