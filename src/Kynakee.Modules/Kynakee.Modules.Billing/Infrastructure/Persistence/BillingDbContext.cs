using System.Linq.Expressions;
using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Billing.Infrastructure.Persistence;

public sealed class BillingDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public BillingDbContext(
        DbContextOptions<BillingDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        _tenantContext = tenantContext;
    }

    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();

    public DbSet<CreditLot> CreditLots => Set<CreditLot>();

    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("schema_billing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        ApplyTenantAndSoftDeleteFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantAndSoftDeleteFilters(ModelBuilder modelBuilder)
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
                Expression.Not(isDeleted),
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