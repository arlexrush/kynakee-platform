using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class AccountActionMigrationTests
{
    [Fact]
    public async Task AccountRecoveryMigrationExistingAccountsReceiveDistinctUsableVersions()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        var db = new IdentityDbContext(options, new AnonymousTenant());
        await using var dbDisposal = db.ConfigureAwait(true);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018121521_AddInvitationEmailJobs", TestContext.Current.CancellationToken);
        var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await using var connectionDisposal = connection.ConfigureAwait(true);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using (var seed = new NpgsqlCommand("INSERT INTO schema_identity.users (id, tenant_id, first_name, last_name, normalized_email, password_hash, status, created_at, updated_at, is_deleted) VALUES (gen_random_uuid(), gen_random_uuid(), 'Legacy', 'One', 'LEGACY1@EXAMPLE.TEST', 'synthetic-hash', 'Active', now(), now(), false), (gen_random_uuid(), gen_random_uuid(), 'Legacy', 'Two', 'LEGACY2@EXAMPLE.TEST', 'synthetic-hash', 'Active', now(), now(), false)", connection))
            await seed.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        using var command = new NpgsqlCommand("SELECT count(*) FROM schema_identity.users WHERE credential_version <> '00000000-0000-0000-0000-000000000000'::uuid AND status = 'Active'", connection);
        var migrated = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2L, migrated);
        using var distinct = new NpgsqlCommand("SELECT count(DISTINCT credential_version) FROM schema_identity.users", connection);
        Assert.Equal(2L, await distinct.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    private sealed record AnonymousTenant : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
    }
}
