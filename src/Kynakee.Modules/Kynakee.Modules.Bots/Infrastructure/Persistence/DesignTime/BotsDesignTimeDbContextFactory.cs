using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kynakee.Modules.Bots.Infrastructure.Persistence.DesignTime;

public sealed class BotsDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<BotsDbContext>
{
    public BotsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__Postgres to generate or apply Bots EF migrations.");
        }

        var options = new DbContextOptionsBuilder<BotsDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsAssembly(typeof(BotsDbContext).Assembly.FullName))
            .Options;
        return new BotsDbContext(options, new BotsDesignTimeTenantContext());
    }
}

internal sealed class BotsDesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;

    public Guid UserId => Guid.Empty;

    public bool IsAuthenticated => false;
}
