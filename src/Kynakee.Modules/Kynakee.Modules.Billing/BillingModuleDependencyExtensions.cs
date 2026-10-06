using FluentValidation;
using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.Billing.Application.Commands.ReserveCredits;
using Kynakee.Modules.Billing.Application.Commands.InitializeCreditAccount;
using Kynakee.Modules.Billing.Application.Options;
using Kynakee.Modules.Billing.Application.Services;
using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.Billing.Infrastructure.Persistence;
using Kynakee.Modules.Billing.Infrastructure.Repositories;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kynakee.Modules.Billing;

public static class BillingModuleDependencyExtensions
{
    public static IServiceCollection AddBillingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres must be configured for the Billing module.");
        }

        services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IBillingRepository, EfBillingRepository>();
        services.AddScoped<IModuleTransactionParticipant, BillingTransactionParticipant>();
        services.AddScoped<BillingService>();
        services.AddScoped<IBillingService>(provider => provider.GetRequiredService<BillingService>());
        services.AddScoped<ITokenGateService>(provider => provider.GetRequiredService<BillingService>());
        services.Configure<BillingOptions>(configuration.GetSection("Billing"));
        services.AddScoped(provider =>
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BillingOptions>>().Value);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddMediatR(options =>
            options.RegisterServicesFromAssembly(typeof(InitializeCreditAccountCommandHandler).Assembly));
        services.AddValidatorsFromAssembly(typeof(InitializeCreditAccountCommandHandler).Assembly);

        return services;
    }
}
