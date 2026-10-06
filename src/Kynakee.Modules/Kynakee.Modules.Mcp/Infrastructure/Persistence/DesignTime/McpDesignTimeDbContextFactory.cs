using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kynakee.Modules.Mcp.Infrastructure.Persistence.DesignTime;

public sealed class McpDesignTimeDbContextFactory : IDesignTimeDbContextFactory<McpDbContext>
{
    public McpDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__Postgres to generate or apply MCP EF migrations.");
        }

        var options = new DbContextOptionsBuilder<McpDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsAssembly(typeof(McpDbContext).Assembly.FullName))
            .Options;
        return new McpDbContext(options, new McpDesignTimeTenantContext());
    }
}

internal sealed class McpDesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;

    public Guid UserId => Guid.Empty;

    public bool IsAuthenticated => false;
}
