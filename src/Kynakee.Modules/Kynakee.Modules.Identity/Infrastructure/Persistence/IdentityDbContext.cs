using System.Linq.Expressions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        _tenantContext = tenantContext;
    }

    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("schema_identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
        ApplyTenantRootFilter(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyTenantRootFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>()
            .HasQueryFilter(tenant => !tenant.IsDeleted && tenant.TenantId == tenant.Id);
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType is not null ||
                entityType.IsOwned() ||
                !IsPersistentEntity(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeleted = Expression.Property(
                parameter,
                nameof(BaseEntity<object>.IsDeleted));
            var tenantId = Expression.Property(
                parameter,
                nameof(BaseEntity<object>.TenantId));
            var currentTenantId = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentTenantId));
            var filter = Expression.AndAlso(
                Expression.Equal(isDeleted, Expression.Constant(false)),
                Expression.Equal(tenantId, currentTenantId));

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(filter, parameter));
        }
    }

    private static bool IsPersistentEntity(Type entityType)
    {
        for (var currentType = entityType;
             currentType is not null;
             currentType = currentType.BaseType)
        {
            if (currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition() == typeof(BaseEntity<>))
            {
                return true;
            }
        }

        return false;
    }
}