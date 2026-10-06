using Kynakee.Modules.Bots.Infrastructure.Persistence;
using Kynakee.Modules.Bots.Domain.Repositories;
using Kynakee.Modules.Bots.Infrastructure.Repositories;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kynakee.Modules.Bots
{
    public static class BotsModuleDependencyExtensions
    {
        public static IServiceCollection AddBotsModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<BotsDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"));
            });

            services.AddScoped<IBotConversationRepository, EfBotConversationRepository>();
            services.AddScoped<IModuleTransactionParticipant,
                BotsTransactionParticipant>();

            return services;
        }
    }
}
