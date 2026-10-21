using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class IdentityPostgresPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public async ValueTask InitializeAsync() =>
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact]
    public async Task EnsureCreatedShouldPersistAndReloadIdentityEntities()
    {
        var tenantContext = new TestTenantContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        using (var context = new IdentityDbContext(options, tenantContext))
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);

            var slug = TenantSlug.Create($"tenant-{Guid.NewGuid():N}").Value!;
            var tenant = Tenant.Create(
                "Integration tenant",
                slug,
                TenantType.Company,
                null,
                null,
                "integration-plan").Value!;
            var email = Email.Create($"{Guid.NewGuid():N}@example.test").Value!;
            var user = User.Create(
                tenant.Id,
                "Integration",
                "User",
                email,
                null,
                "test-password-hash").Value!;
            var membership = TenantUser.Create(user, UserRole.Owner).Value!;
            var refreshToken = RefreshToken.Create(
                tenant.Id,
                user.Id,
                new string('a', 64),
                DateTimeOffset.UtcNow).Value!;

            tenantContext.TenantId = tenant.Id;
            context.Tenants.Add(tenant);
            context.Users.Add(user);
            context.TenantUsers.Add(membership);
            context.RefreshTokens.Add(refreshToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (var context = new IdentityDbContext(options, tenantContext))
        {
            var persistedTenant = await context.Tenants.SingleAsync(cancellationToken);
            var persistedUser = await context.Users.SingleAsync(cancellationToken);
            var persistedMembership = await context.TenantUsers.SingleAsync(cancellationToken);
            var persistedRefreshToken = await context.RefreshTokens.SingleAsync(cancellationToken);

            persistedTenant.Id.Should().Be(tenantContext.TenantId);
            persistedTenant.Slug.Value.Should().StartWith("tenant-");
            persistedUser.Email.Value.Should().EndWith("@EXAMPLE.TEST");
            persistedMembership.Role.Should().Be(UserRole.Owner);
            persistedRefreshToken.TokenHash.Should().Be(new string('a', 64));
        }
    }

    [Fact]
    public async Task DuplicateIdentityValuesShouldViolateNamedUniqueConstraints()
    {
        var tenantContext = new TestTenantContext();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = DateTimeOffset.UtcNow;
        var slug = TenantSlug.Create($"tenant-{Guid.NewGuid():N}").Value!;
        var tenant = Tenant.Create("Integration tenant", slug, TenantType.Company, null, null, "integration-plan").Value!;
        var email = Email.Create($"{Guid.NewGuid():N}@example.test").Value!;
        var user = User.Create(tenant.Id, "Integration", "User", email, null, "test-password-hash").Value!;

        tenantContext.TenantId = tenant.Id;
        using (var context = new IdentityDbContext(options, tenantContext))
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            context.Tenants.Add(tenant);
            context.Users.Add(user);
            context.TenantUsers.Add(TenantUser.Create(user, UserRole.Owner).Value!);
            context.RefreshTokens.Add(RefreshToken.Create(tenant.Id, user.Id, new string('a', 64), now).Value!);
            context.UserInvitations.Add(UserInvitation.Create(tenant.Id, user.Id, new string('b', 64), now).Value!);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await AssertUniqueViolationAsync(
            options,
            tenantContext,
            "ux_identity_users_normalized_email",
            context => context.Users.Add(User.Create(
                tenant.Id,
                "Duplicate",
                "Email",
                email,
                null,
                "test-password-hash").Value!));
        await AssertUniqueViolationAsync(
            options,
            tenantContext,
            "ux_identity_tenants_slug",
            context => context.Tenants.Add(Tenant.Create(
                "Duplicate slug",
                slug,
                TenantType.Company,
                null,
                null,
                "integration-plan").Value!));
        await AssertUniqueViolationAsync(
            options,
            tenantContext,
            "ux_identity_tenant_users_user",
            context => context.TenantUsers.Add(TenantUser.Create(user, UserRole.Viewer).Value!));
        await AssertUniqueViolationAsync(
            options,
            tenantContext,
            "ux_identity_refresh_tokens_hash",
            context => context.RefreshTokens.Add(RefreshToken.Create(
                tenant.Id,
                user.Id,
                new string('a', 64),
                now).Value!));
        await AssertUniqueViolationAsync(
            options,
            tenantContext,
            "ux_identity_invitations_hash",
            context => context.UserInvitations.Add(UserInvitation.Create(
                tenant.Id,
                user.Id,
                new string('b', 64),
                now).Value!));
    }

    [Fact]
    public async Task ConcurrentDuplicateEmailsShouldViolateUniqueConstraint()
    {
        var tenantContext = new TestTenantContext();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var tenant = Tenant.Create(
            "Integration tenant",
            TenantSlug.Create($"tenant-{Guid.NewGuid():N}").Value!,
            TenantType.Company,
            null,
            null,
            "integration-plan").Value!;
        var email = Email.Create($"{Guid.NewGuid():N}@example.test").Value!;
        tenantContext.TenantId = tenant.Id;

        using (var context = new IdentityDbContext(options, tenantContext))
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var insertTasks = CreateConcurrentInsertTasks(
            options,
            tenantContext,
            context => context.Users.Add(User.Create(
                tenant.Id,
                "Concurrent",
                "User",
                email,
                null,
                "test-password-hash").Value!));
        var outcomes = await Task.WhenAll(insertTasks);

        outcomes.Count(constraintName => constraintName is null).Should().Be(1);
        outcomes.Single(constraintName => constraintName is not null).Should().Be("ux_identity_users_normalized_email");
    }

    [Fact]
    public async Task ConcurrentDuplicateTenantSlugsShouldViolateUniqueConstraint()
    {
        var tenantContext = new TestTenantContext();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var slug = TenantSlug.Create($"tenant-{Guid.NewGuid():N}").Value!;

        using (var context = new IdentityDbContext(options, tenantContext))
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        var insertTasks = CreateConcurrentInsertTasks(
            options,
            tenantContext,
            context => context.Tenants.Add(Tenant.Create(
                "Concurrent tenant",
                slug,
                TenantType.Company,
                null,
                null,
                "integration-plan").Value!));
        var outcomes = await Task.WhenAll(insertTasks);

        outcomes.Count(constraintName => constraintName is null).Should().Be(1);
        outcomes.Single(constraintName => constraintName is not null).Should().Be("ux_identity_tenants_slug");
    }

    [Fact]
#pragma warning disable CA2007 // xUnit requiere conservar el contexto de ejecución de la prueba.
    public async Task ConcurrentIdempotencyRequestsShouldWaitAndReplayCommittedOutcome()
    {
        var tenantContext = new TestTenantContext();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = new IdempotencyRequest(
            new string('a', 64),
            new string('b', 64),
            Guid.Empty,
            Guid.Empty,
            "identity.login");

        await using (var setupContext = new IdentityDbContext(options, tenantContext))
        {
            await setupContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        await using var firstContext = new IdentityDbContext(options, tenantContext);
        await using var secondContext = new IdentityDbContext(options, tenantContext);
        var firstParticipant = new IdentityTransactionParticipant(firstContext);
        var secondParticipant = new IdentityTransactionParticipant(secondContext);
        await using var firstTransaction = await firstParticipant.BeginTransactionAsync(cancellationToken);
        await using var secondTransaction = await secondParticipant.BeginTransactionAsync(cancellationToken);
        var firstScope = firstTransaction.Idempotency!;
        var secondScope = secondTransaction.Idempotency!;

        await firstScope.AcquireLockAsync(request, cancellationToken);
        var secondLock = secondScope.AcquireLockAsync(request, cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        secondLock.IsCompleted.Should().BeFalse();

        var encryptedOutcome = new byte[] { 1, 2, 3, 4 };
        await firstScope.StoreAsync(request, encryptedOutcome, DateTimeOffset.UtcNow.AddHours(24), cancellationToken);
        await firstTransaction.CommitAsync(cancellationToken);
        await secondLock;

        var replay = await secondScope.AcquireAsync(request, cancellationToken);

        replay.Exists.Should().BeTrue();
        replay.FingerprintMatches.Should().BeTrue();
        replay.EncryptedOutcome!.Value.ToArray().Should().Equal(encryptedOutcome);
        await secondTransaction.CommitAsync(cancellationToken);
    }
#pragma warning restore CA2007

    private static async Task AssertUniqueViolationAsync(
        DbContextOptions<IdentityDbContext> options,
        TestTenantContext tenantContext,
        string expectedConstraint,
        Action<IdentityDbContext> addDuplicate)
    {
        using var context = new IdentityDbContext(options, tenantContext);
        addDuplicate(context);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken)).ConfigureAwait(false);

        exception.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be(expectedConstraint);
    }

    private static Task<string?>[] CreateConcurrentInsertTasks(
        DbContextOptions<IdentityDbContext> options,
        TestTenantContext tenantContext,
        Action<IdentityDbContext> addEntity)
    {
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstInsert = InsertAfterSignalAsync(options, tenantContext, startSignal.Task, addEntity);
        var secondInsert = InsertAfterSignalAsync(options, tenantContext, startSignal.Task, addEntity);
        startSignal.SetResult();
        return [firstInsert, secondInsert];
    }

    private static async Task<string?> InsertAfterSignalAsync(
        DbContextOptions<IdentityDbContext> options,
        TestTenantContext tenantContext,
        Task startSignal,
        Action<IdentityDbContext> addEntity)
    {
        using var context = new IdentityDbContext(options, tenantContext);
        addEntity(context);
        await startSignal.ConfigureAwait(false);

        try
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            return null;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.ConstraintName;
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; }

        public Guid UserId { get; } = Guid.NewGuid();

        public bool IsAuthenticated => true;
    }
}
