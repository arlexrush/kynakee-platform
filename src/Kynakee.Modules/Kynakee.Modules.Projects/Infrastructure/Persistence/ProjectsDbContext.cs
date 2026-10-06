using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence
{
    public sealed class ProjectsDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;

        public Guid CurrentTenantId => _tenantContext.TenantId;

        public ProjectsDbContext(
            DbContextOptions<ProjectsDbContext> options,
            ITenantContext tenantContext)
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(tenantContext);

            _tenantContext = tenantContext;
        }

        public DbSet<Project> Projects =>
            Set<Project>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.HasDefaultSchema("schema_projects");

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ProjectsDbContext).Assembly);

            ApplyGlobalFilters(modelBuilder);

            base.OnModelCreating(modelBuilder); 
        }

        private void ApplyGlobalFilters(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Los tipos derivados heredan el filtro definido en la raíz TPH.
                // Los value objects poseídos no son entidades persistentes independientes.
                if (entityType.BaseType is not null ||
                    entityType.IsOwned() ||
                    !IsPersistentEntity(entityType.ClrType))
                {
                    continue;
                }

                var parameter = Expression.Parameter(
                    entityType.ClrType,
                    "entity");

                var isDeleted = Expression.Property(
                    parameter,
                    nameof(BaseEntity<object>.IsDeleted));

                var tenantId = Expression.Property(
                    parameter,
                    nameof(BaseEntity<object>.TenantId));

                var currentTenantId = Expression.Property(
                    Expression.Constant(this),
                    nameof(CurrentTenantId));

                var body = Expression.AndAlso(
                    Expression.Equal(
                        isDeleted,
                        Expression.Constant(false)),
                    Expression.Equal(
                        tenantId,
                        currentTenantId));

                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(
                        Expression.Lambda(body, parameter));
            }
        }

        /// <summary>
        /// Determina si el tipo especificado o cualquiera de sus tipos base deriva de BaseEntity<T>.
        /// </summary>
        /// <remarks>La comprobación recorre la jerarquía de herencia examinando tipos genéricos cuyo tipo
        /// genérico es BaseEntity<>.</remarks>
        /// <param name="entityType">Tipo a inspeccionar para determinar si deriva de BaseEntity<T>.</param>
        /// <returns>true si el tipo o alguno de sus tipos base es una instanciación de BaseEntity<T>; false en caso contrario.</returns>
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
}
