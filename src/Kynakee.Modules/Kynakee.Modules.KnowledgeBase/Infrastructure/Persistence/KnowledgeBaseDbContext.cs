using Kynakee.Modules.KnowledgeBase.Domain.Aggregates;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Kynakee.Modules.KnowledgeBase.Infrastructure.Persistence;

public sealed class KnowledgeBaseDbContext : DbContext
{
    public KnowledgeBaseDbContext(DbContextOptions<KnowledgeBaseDbContext> options)
        : base(options)
    {
    }

    public DbSet<CanonicalConcept> CanonicalConcepts => Set<CanonicalConcept>();

    public DbSet<APUTemplate> APUTemplates => Set<APUTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("schema_knowledge_base");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnowledgeBaseDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType is not null ||
                entityType.IsOwned() ||
                !IsGlobalEntity(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeleted = Expression.Property(
                parameter,
                nameof(GlobalEntity<object>.IsDeleted));
            var filter = Expression.Equal(isDeleted, Expression.Constant(false));

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(filter, parameter));
        }
    }

    private static bool IsGlobalEntity(Type entityType)
    {
        for (var currentType = entityType;
             currentType is not null;
             currentType = currentType.BaseType)
        {
            if (currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition() == typeof(GlobalEntity<>))
            {
                return true;
            }
        }

        return false;
    }
}