using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Mcp.Infrastructure.Persistence;

public sealed class McpDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public McpDbContext(
        DbContextOptions<McpDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        _tenantContext = tenantContext;
    }

    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<McpProvider> Providers => Set<McpProvider>();

    public DbSet<McpServer> Servers => Set<McpServer>();

    public DbSet<McpQueryLog> QueryLogs => Set<McpQueryLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("schema_mcp");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(McpDbContext).Assembly);
        modelBuilder.Entity<McpProvider>()
            .HasQueryFilter(provider =>
                !provider.IsDeleted && provider.TenantId == CurrentTenantId);
        modelBuilder.Entity<McpServer>()
            .HasQueryFilter(server =>
                !server.IsDeleted && server.TenantId == CurrentTenantId);
        modelBuilder.Entity<McpQueryLog>()
            .HasQueryFilter(queryLog =>
                !queryLog.IsDeleted && queryLog.TenantId == CurrentTenantId);

        base.OnModelCreating(modelBuilder);
    }
}
