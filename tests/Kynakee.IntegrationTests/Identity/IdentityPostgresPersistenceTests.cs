using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
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

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; }

        public Guid UserId { get; } = Guid.NewGuid();

        public bool IsAuthenticated => true;
    }
}
