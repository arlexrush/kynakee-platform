using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.DesignTime;

public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__Postgres to generate or apply Identity EF migrations.");
        }

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;
        return new IdentityDbContext(options, new IdentityDesignTimeTenantContext());
    }
}
