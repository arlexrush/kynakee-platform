using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.Mcp.Infrastructure.Repositories;
using Kynakee.Modules.Mcp.Contracts;
using Kynakee.Modules.Mcp.Infrastructure.Client;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Kynakee.Modules.Mcp.Application.Commands.RegisterMcpProvider;

namespace Kynakee.Modules.Mcp
{
    public static class McpModuleDependencyExtensions
    {
        public static IServiceCollection AddMcpModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<McpDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"));
            });
            services.AddScoped<IMcpProviderRepository, EfMcpProviderRepository>();
            services.AddScoped<IMcpQueryLogRepository, EfMcpQueryLogRepository>();
            services.AddValidatorsFromAssemblyContaining<RegisterMcpProviderCommandValidator>();
            services.AddSingleton(new McpServerResilience());
            services.AddScoped(provider => new McpQueryAuditWriter(
                provider.GetRequiredService<DbContextOptions<McpDbContext>>(),
                provider.GetRequiredService<ITenantContext>()));
            services.AddHttpClient("mcp-provider", client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    UseProxy = false,
                    ConnectTimeout = TimeSpan.FromSeconds(10)
                });
            services.AddScoped<IMCPClient>(provider => new ProviderNetworkClient(
                provider.GetRequiredService<IMcpProviderRepository>(),
                provider.GetRequiredService<IHttpClientFactory>(),
                configuration,
                provider.GetRequiredService<McpServerResilience>(),
                provider.GetRequiredService<McpQueryAuditWriter>(),
                provider.GetRequiredService<ITenantContext>()));
            services.AddScoped<IModuleTransactionParticipant,
                McpTransactionParticipant>();
            return services;
        }
    }
}
