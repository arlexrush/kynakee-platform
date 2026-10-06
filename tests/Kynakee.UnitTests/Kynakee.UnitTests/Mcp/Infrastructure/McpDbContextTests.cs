using FluentAssertions;
using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kynakee.UnitTests.Mcp.Infrastructure;

public sealed class McpDbContextTests
{
    [Fact]
    public void ModelShouldUseMcpSchema()
    {
        using var context = CreateContext();

        context.Model.GetDefaultSchema().Should().Be("schema_mcp");
    }

    [Fact]
    public void TenantEntitiesShouldHaveSoftDeleteAndTenantFilters()
    {
        using var context = CreateContext();
        var providerQuery = context.Providers.AsNoTracking().ToQueryString();
        var serverQuery = context.Servers.AsNoTracking().ToQueryString();
        var queryLogQuery = context.QueryLogs.AsNoTracking().ToQueryString();

        providerQuery.Should().Contain("is_deleted").And.Contain("tenant_id");
        serverQuery.Should().Contain("is_deleted").And.Contain("tenant_id");
        queryLogQuery.Should().Contain("is_deleted").And.Contain("tenant_id");
    }

    [Fact]
    public void ProviderShouldUseXminAndServerShouldHaveTenantScopedForeignKey()
    {
        using var context = CreateContext();
        var providerVersion = context.Model.FindEntityType(typeof(McpProvider))!
            .FindProperty("Version")!;
        var serverForeignKey = context.Model.FindEntityType(typeof(McpServer))!
            .GetForeignKeys()
            .Should().ContainSingle()
            .Which;

        providerVersion.IsConcurrencyToken.Should().BeTrue();
        providerVersion.GetColumnName().Should().Be("xmin");
        serverForeignKey.Properties.Select(property => property.Name)
            .Should().ContainInOrder(nameof(McpServer.TenantId), nameof(McpServer.ProviderId));
        serverForeignKey.PrincipalEntityType.ClrType.Should().Be<McpProvider>();
    }

    [Fact]
    public void QueryLogsShouldUseTenantScopedProjectIndex()
    {
        using var context = CreateContext();
        var indexes = context.Model.FindEntityType(typeof(McpQueryLog))!.GetIndexes();

        indexes.Should().Contain(index => index.GetDatabaseName() == "idx_mcp_query_logs_project");
    }

    private static McpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<McpDbContext>()
            .UseNpgsql("Host=localhost;Database=mcp-tests;Username=test;Password=test")
            .Options;
        return new McpDbContext(options, new TestTenantContext());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();

        public Guid UserId { get; } = Guid.NewGuid();

        public bool IsAuthenticated => true;
    }
}
