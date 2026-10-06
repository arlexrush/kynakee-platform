using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public class IdentityDbContextTests
{
    [Fact]
    public void ModelShouldUseIdentitySchema()
    {
        using var context = CreateContext();

        context.Model.GetDefaultSchema().Should().Be("schema_identity");
    }

    [Fact]
    public void TenantAndUserShouldHaveGlobalIsolationFilters()
    {
        using var context = CreateContext();
        var tenantQuery = context.Tenants.AsNoTracking().ToQueryString();
        var userQuery = context.Users.AsNoTracking().ToQueryString();

        tenantQuery.Should().Contain("is_deleted").And.Contain("tenant_id");
        userQuery.Should().Contain("is_deleted").And.Contain("tenant_id");
    }

    [Fact]
    public void TenantAndUserShouldUseXminConcurrencyToken()
    {
        using var context = CreateContext();
        var tenantVersion = context.Model.FindEntityType(typeof(Tenant))!
            .FindProperty("Version")!;
        var userVersion = context.Model.FindEntityType(typeof(User))!
            .FindProperty("Version")!;

        tenantVersion.IsConcurrencyToken.Should().BeTrue();
        tenantVersion.GetColumnName().Should().Be("xmin");
        userVersion.IsConcurrencyToken.Should().BeTrue();
        userVersion.GetColumnName().Should().Be("xmin");
    }

    [Fact]
    public void RefreshTokenInvitationAndMembershipShouldUseXminConcurrencyToken()
    {
        using var context = CreateContext();

        foreach (var entityType in new[] { typeof(RefreshToken), typeof(UserInvitation), typeof(TenantUser) })
        {
            var version = context.Model.FindEntityType(entityType)!.FindProperty("Version")!;

            version.IsConcurrencyToken.Should().BeTrue();
            version.GetColumnName().Should().Be("xmin");
        }
    }

    [Fact]
    public void TenantUserShouldHaveTenantScopedUserForeignKey()
    {
        using var context = CreateContext();
        var foreignKey = context.Model.FindEntityType(typeof(TenantUser))!
            .GetForeignKeys()
            .Should().ContainSingle()
            .Which;

        foreignKey.Properties.Select(property => property.Name)
            .Should().ContainInOrder(nameof(TenantUser.TenantId), nameof(TenantUser.UserId));
        foreignKey.PrincipalEntityType.ClrType.Should().Be<User>();
    }

    [Fact]
    public void UserEmailIndexShouldBeUnique()
    {
        using var context = CreateContext();
        var emailNavigation = context.Model.FindEntityType(typeof(User))!
            .FindNavigation(nameof(User.Email))!;

        emailNavigation.TargetEntityType.GetIndexes()
            .Should().ContainSingle(index => index.IsUnique);
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=identity-tests;Username=test;Password=test")
            .Options;
        return new IdentityDbContext(options, new TestTenantContext());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();

        public Guid UserId { get; } = Guid.NewGuid();

        public bool IsAuthenticated => true;
    }
}