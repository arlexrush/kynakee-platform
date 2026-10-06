using FluentAssertions;
using Kynakee.Modules.Mcp;
using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Kynakee.Modules.Mcp.Infrastructure.Repositories;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Mcp;

public sealed class McpPostgresPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly TestTenantContext _tenantContext = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var context = CreateContext();
        await using var lifetime = context.ConfigureAwait(false);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    [Fact]
    public async Task MigrationFreshDatabaseShouldApplyMcpSchemaAndOptionalProject()
    {
        await using var context = CreateContext();

        var migrations = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        migrations.Select(migration => migration[(migration.IndexOf('_', StringComparison.Ordinal) + 1)..])
            .Should().Equal("InitialMcpSchema", "OptionalMcpQueryProject");
    }

    [Fact]
    public void MigrationCurrentModelShouldHaveNoPendingChanges()
    {
        using var context = CreateContext();

        context.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public async Task ProviderRoundTripShouldPreserveCatalogAndAudit()
    {
        var provider = NewProvider();
        provider.UpdateRating(4.25m, _tenantContext.UserId);
        await SeedAsync(provider);
        await using var context = CreateContext();

        var loaded = await new EfMcpProviderRepository(context)
            .GetByIdForUpdateAsync(provider.Id, TestContext.Current.CancellationToken);

        loaded.Should().BeEquivalentTo(new
        {
            provider.Id,
            provider.TenantId,
            provider.Name,
            provider.Categories,
            provider.GeoRegions,
            provider.Rating,
            provider.Status,
            provider.CreatedBy,
            provider.UpdatedBy,
            provider.IsDeleted
        });
    }

    [Fact]
    public async Task ProviderReloadForUpdateShouldLoadServerAndPersistSoftDelete()
    {
        var provider = NewProvider();
        var server = AddServer(provider);
        await SeedAsync(provider);
        await using (var context = CreateContext())
        {
            var loaded = await new EfMcpProviderRepository(context)
                .GetByIdForUpdateAsync(provider.Id, TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException();
            loaded.RemoveServer(server.Id, _tenantContext.UserId);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var verification = CreateContext();

        var stored = await verification.Servers.IgnoreQueryFilters()
            .SingleAsync(TestContext.Current.CancellationToken);

        stored.Should().BeEquivalentTo(new
        {
            server.Id,
            server.ProviderId,
            server.TenantId,
            server.Endpoint,
            server.CredentialSecretReference,
            IsDeleted = true,
            UpdatedBy = (Guid?)_tenantContext.UserId
        });
    }

    [Fact]
    public async Task ProviderFilterDifferentTenantShouldHideOwnedProvider()
    {
        var provider = NewProvider();
        await SeedAsync(provider);
        _tenantContext.TenantId = Guid.NewGuid();
        await using var context = CreateContext();

        var loaded = await new EfMcpProviderRepository(context)
            .GetByIdForUpdateAsync(provider.Id, TestContext.Current.CancellationToken);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task CatalogQueryDifferentTenantShouldReturnAvailableProviderAndLiveServers()
    {
        var provider = NewProvider();
        var liveServer = AddServer(provider);
        var deletedServer = provider.AddServer(new Uri("https://deleted.example.test/mcp"), "secrets/deleted").Value
            ?? throw new InvalidOperationException();
        provider.RemoveServer(deletedServer.Id);
        await SeedAsync(provider);
        _tenantContext.TenantId = Guid.NewGuid();
        await using var context = CreateContext();

        var available = await new EfMcpProviderRepository(context)
            .GetAvailableForQueryAsync("es-vc", "ceramics", DateTime.UtcNow, TestContext.Current.CancellationToken);

        available.Should().ContainSingle().Which.Servers.Select(server => server.Id).Should().Equal(liveServer.Id);
    }

    [Fact]
    public async Task CatalogQueryDeletedProviderShouldExcludeProvider()
    {
        var provider = NewProvider();
        AddServer(provider);
        provider.Delete(_tenantContext.UserId);
        await SeedAsync(provider);
        await using var context = CreateContext();

        var available = await new EfMcpProviderRepository(context)
            .GetAvailableForQueryAsync("ES-VC", "ceramics", DateTime.UtcNow, TestContext.Current.CancellationToken);

        available.Should().BeEmpty();
    }

    [Theory]
    [InlineData(McpProviderStatus.Suspended)]
    [InlineData(McpProviderStatus.Testing)]
    public async Task CatalogQueryUnavailableProviderShouldExcludeProvider(McpProviderStatus status)
    {
        var provider = NewProvider();
        AddServer(provider);
        provider.ChangeStatus(status);
        await SeedAsync(provider);
        await using var context = CreateContext();

        var available = await new EfMcpProviderRepository(context)
            .GetAvailableForQueryAsync("ES-VC", "ceramics", DateTime.UtcNow, TestContext.Current.CancellationToken);

        available.Should().BeEmpty();
    }

    [Fact]
    public async Task CatalogQueryExpiredCooldownShouldIncludeProvider()
    {
        var provider = NewProvider();
        AddServer(provider);
        var occurredAt = DateTime.UtcNow.AddMinutes(-1);
        provider.RecordFailure(occurredAt);
        provider.RecordFailure(occurredAt);
        provider.RecordFailure(occurredAt);
        await SeedAsync(provider);
        await using var context = CreateContext();

        var available = await new EfMcpProviderRepository(context)
            .GetAvailableForQueryAsync("ES-VC", "ceramics", DateTime.UtcNow, TestContext.Current.CancellationToken);

        available.Select(candidate => candidate.Id).Should().Equal(provider.Id);
    }

    [Fact]
    public async Task OwnedListRegionAndCategoryShouldMatchNormalizedArrays()
    {
        var provider = NewProvider();
        await SeedAsync(provider);
        await using var context = CreateContext();

        var page = await new EfMcpProviderRepository(context)
            .ListOwnedAsync("es-vc", "ceramics", McpProviderStatus.Active, 0, 10, TestContext.Current.CancellationToken);

        page.Select(candidate => candidate.Id).Should().Equal(provider.Id);
    }

    [Fact]
    public async Task ServerUniquenessDuplicateEndpointShouldRejectAssociation()
    {
        var original = NewProvider();
        AddServer(original);
        await SeedAsync(original);
        var duplicate = NewProvider();
        AddServer(duplicate);
        await using var context = CreateContext();
        await new EfMcpProviderRepository(context).AddAsync(duplicate, TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        error.InnerException.Should().BeEquivalentTo(new
        {
            SqlState = PostgresErrorCodes.UniqueViolation,
            ConstraintName = "idx_mcp_servers_endpoint"
        });
    }

    [Fact]
    public async Task ServerForeignKeyDifferentTenantShouldRejectAssociation()
    {
        var provider = NewProvider();
        await SeedAsync(provider);
        await using var context = CreateContext();
        var serverId = Guid.NewGuid();
        var wrongTenant = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO schema_mcp.mcp_servers (id, tenant_id, provider_id, endpoint, credential_secret_reference, created_at, updated_at, is_deleted) VALUES ({serverId}, {wrongTenant}, {provider.Id.Value}, 'https://wrong.example.test/mcp', 'secrets/wrong', NOW(), NOW(), FALSE)",
            TestContext.Current.CancellationToken));

        error.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task QueryLogRoundTripDifferentProviderTenantShouldPreserveConsumerAudit()
    {
        var provider = NewProvider();
        await SeedAsync(provider);
        _tenantContext.TenantId = Guid.NewGuid();
        var queryLog = NewQueryLog(provider.Id);
        await using (var context = CreateContext())
        {
            await new EfMcpQueryLogRepository(context).AddAsync(queryLog, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var verification = CreateContext();

        var loaded = await new EfMcpQueryLogRepository(verification)
            .GetByProviderIdAsync(provider.Id, 0, 10, TestContext.Current.CancellationToken);

        loaded.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            queryLog.Id,
            queryLog.TenantId,
            queryLog.ProjectId,
            queryLog.ProviderId,
            queryLog.CanonicalConceptId,
            queryLog.Quantity,
            queryLog.Unit,
            queryLog.ResponsePrice,
            queryLog.ResponseUnit,
            queryLog.FallbackSource,
            queryLog.Confidence,
            queryLog.Status,
            queryLog.CreatedBy
        });
    }

    [Fact]
    public async Task QueryLogFilterDifferentTenantShouldHideConsumerData()
    {
        var provider = NewProvider();
        await SeedAsync(provider);
        await using (var context = CreateContext())
        {
            await new EfMcpQueryLogRepository(context).AddAsync(NewQueryLog(provider.Id), TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        _tenantContext.TenantId = Guid.NewGuid();
        await using var verification = CreateContext();

        var logs = await new EfMcpQueryLogRepository(verification)
            .GetByProviderIdAsync(provider.Id, 0, 10, TestContext.Current.CancellationToken);

        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryLogRoundTripWithoutProjectShouldPreserveNullProject()
    {
        var provider = NewProvider();
        await SeedAsync(provider);
        var queryLog = McpQueryLog.Create(
            _tenantContext.TenantId, null, provider.Id, "concept-1", McpComponentType.Material,
            2m, "kg", 7.25m, "kg", null, 0.9m, 100, McpQueryStatus.Success, 0m,
            DateTime.UtcNow, _tenantContext.UserId).Value ?? throw new InvalidOperationException();
        await using (var context = CreateContext())
        {
            await new EfMcpQueryLogRepository(context).AddAsync(queryLog, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var verification = CreateContext();

        var stored = await verification.QueryLogs.SingleAsync(TestContext.Current.CancellationToken);

        stored.ProjectId.Should().BeNull();
    }

    [Fact]
    public async Task MigrationExistingProjectQueryShouldPreserveProject()
    {
        await using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261002015154_InitialMcpSchema", TestContext.Current.CancellationToken);
        var provider = NewProvider();
        var queryLog = NewQueryLog(provider.Id);
        await new EfMcpProviderRepository(context).AddAsync(provider, TestContext.Current.CancellationToken);
        await new EfMcpQueryLogRepository(context).AddAsync(queryLog, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var stored = await verification.QueryLogs.SingleAsync(TestContext.Current.CancellationToken);

        stored.ProjectId.Should().Be(queryLog.ProjectId);
    }

    [Fact]
    public async Task ProviderConcurrencyStaleWriteShouldFail()
    {
        await SeedAsync(NewProvider());
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = await firstContext.Providers.SingleAsync(TestContext.Current.CancellationToken);
        var second = await secondContext.Providers.SingleAsync(TestContext.Current.CancellationToken);
        first.UpdateRating(4m);
        second.UpdateRating(3m);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Func<Task> save = () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task TransactionCommitShouldPersistProviderAndServer()
    {
        await using var context = CreateContext();
        var transaction = await new McpTransactionParticipant(context)
            .BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var lifetime = transaction.ConfigureAwait(true);
        var provider = NewProvider();
        AddServer(provider);
        await new EfMcpProviderRepository(context).AddAsync(provider, TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var providers = await verification.Providers.CountAsync(TestContext.Current.CancellationToken);
        var servers = await verification.Servers.CountAsync(TestContext.Current.CancellationToken);

        (providers, servers).Should().Be((1, 1));
    }

    [Fact]
    public async Task TransactionRollbackShouldDiscardSavedRowsAndTracking()
    {
        await using var context = CreateContext();
        var transaction = await new McpTransactionParticipant(context)
            .BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var lifetime = transaction.ConfigureAwait(true);
        await new EfMcpProviderRepository(context).AddAsync(NewProvider(), TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var count = await verification.Providers.CountAsync(TestContext.Current.CancellationToken);

        (count, context.ChangeTracker.Entries().Count()).Should().Be((0, 0));
    }

    [Fact]
    public async Task ParticipantClearSnapshotShouldPreserveNewEvents()
    {
        await using var context = CreateContext();
        var provider = NewProvider();
        provider.RecordFailure();
        provider.RecordFailure();
        provider.RecordFailure();
        await new EfMcpProviderRepository(context).AddAsync(provider, TestContext.Current.CancellationToken);
        var participant = new McpTransactionParticipant(context);
        var snapshot = participant.CollectDomainEvents();
        provider.ChangeStatus(McpProviderStatus.Active);
        provider.RecordFailure();
        provider.RecordFailure();
        provider.RecordFailure();

        participant.ClearDomainEvents(snapshot);

        participant.CollectDomainEvents().Should().ContainSingle().Which.Should().NotBe(snapshot.Single());
    }

    [Fact]
    public void DependencyRegistrationShouldResolveConcreteScopedImplementations()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString()
        }).Build();
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext>(_ => _tenantContext);
        services.AddMcpModule(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IMcpProviderRepository>();
        var logs = scope.ServiceProvider.GetRequiredService<IMcpQueryLogRepository>();
        var participant = scope.ServiceProvider.GetRequiredService<IModuleTransactionParticipant>();

        (repository.GetType(), logs.GetType(), participant.GetType()).Should().Be(
            (typeof(EfMcpProviderRepository), typeof(EfMcpQueryLogRepository), typeof(McpTransactionParticipant)));
    }

    private McpDbContext CreateContext() => new(
        new DbContextOptionsBuilder<McpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
        _tenantContext);

    private McpProvider NewProvider() =>
        McpProvider.Create(_tenantContext.TenantId, "Provider", ["ceramics"], ["ES-VC"], _tenantContext.UserId).Value
        ?? throw new InvalidOperationException();

    private static McpServer AddServer(McpProvider provider) =>
        provider.AddServer(new Uri("https://provider.example.test/mcp"), "secrets/provider").Value
        ?? throw new InvalidOperationException();

    private McpQueryLog NewQueryLog(McpProviderId providerId) =>
        McpQueryLog.Create(_tenantContext.TenantId, Guid.NewGuid(), providerId, "concept-1",
            McpComponentType.Material, 2m, "kg", 7.25m, "kg", null, 0.9m, 100,
            McpQueryStatus.Success, 0m, DateTime.UtcNow, _tenantContext.UserId).Value
        ?? throw new InvalidOperationException();

    private async Task SeedAsync(McpProvider provider)
    {
        var context = CreateContext();
        await using var lifetime = context.ConfigureAwait(false);
        await new EfMcpProviderRepository(context).AddAsync(provider, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; } = Guid.NewGuid();

        public Guid UserId { get; } = Guid.NewGuid();

        public bool IsAuthenticated => true;
    }
}
