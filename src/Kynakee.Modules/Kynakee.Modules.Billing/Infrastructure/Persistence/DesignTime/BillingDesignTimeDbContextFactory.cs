using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kynakee.Modules.Billing.Infrastructure.Persistence.DesignTime;

public sealed class BillingDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__Postgres to generate or apply Billing EF migrations.");
        }

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName))
            .Options;
        return new BillingDbContext(options, new BillingDesignTimeTenantContext());
    }
}