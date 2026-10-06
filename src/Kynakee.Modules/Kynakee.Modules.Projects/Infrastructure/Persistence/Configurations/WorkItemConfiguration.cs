using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations
{
    public sealed class WorkItemConfiguration :
    IEntityTypeConfiguration<WorkItem>
    {
        public void Configure(
            EntityTypeBuilder<WorkItem> builder)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            builder.ToTable("work_items");

            builder.HasKey(workItem => workItem.Id);

            builder.Property(workItem => workItem.Id)
                .HasColumnName("id")
                .HasConversion(
                    id => id.Value,
                    value => new WorkItemId(value))
                .ValueGeneratedNever();

            builder.Property(workItem => workItem.ProjectId)
                .HasColumnName("project_id")
                .HasConversion(
                    id => id.Value,
                    value => new ProjectId(value))
                .IsRequired();

            builder.Property(workItem => workItem.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();

            builder.Property(workItem => workItem.CanonicalConceptId)
                .HasColumnName("canonical_concept_id")
                .HasConversion(
                    concept => concept.Value,
                    value => new CanonicalConceptId(value))
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(workItem => workItem.Description)
                .HasColumnName("description")
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(workItem => workItem.Unit)
                .HasColumnName("unit")
                .HasConversion(
                    unit => unit.Code,
                    code => ResolveMeasurementUnit(code))
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(workItem => workItem.Quantity)
                .HasColumnName("quantity")
                .HasPrecision(12, 4)
                .IsRequired();

            builder.Property(workItem => workItem.Location)
                .HasColumnName("location")
                .HasMaxLength(200);

            builder.Property(workItem => workItem.Observations)
                .HasColumnName("observations");

            builder.Property(workItem => workItem.Confidence)
                .HasColumnName("confidence")
                .HasConversion(
                    confidence => confidence.Value,
                    value => new Confidence(value))
                .HasPrecision(4, 3);

            builder.Property(workItem => workItem.AIStatus)
                .HasColumnName("ai_status")
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(workItem => workItem.SortOrder)
                .HasColumnName("sort_order")
                .IsRequired();

            builder.Property(workItem => workItem.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(workItem => workItem.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            builder.Property(workItem => workItem.CreatedBy)
                .HasColumnName("created_by");

            builder.Property(workItem => workItem.UpdatedBy)
                .HasColumnName("updated_by");

            builder.Property(workItem => workItem.DeletedAt)
                .HasColumnName("deleted_at");

            builder.Property(workItem => workItem.IsDeleted)
                .HasColumnName("is_deleted")
                .IsRequired();

            builder.Property(workItem => workItem.Version)
                .HasColumnName("xmin")
                .IsRowVersion()
                .IsConcurrencyToken();




            builder.HasAlternateKey(workItem => new
            {
                workItem.Id,
                workItem.ProjectId,
                workItem.TenantId
            })
            .HasName("ak_work_items_id_project_tenant");

            builder.HasOne<Project>()
                .WithMany(project => project.WorkItems)
                .HasForeignKey(workItem => new
                {
                    workItem.ProjectId,
                    workItem.TenantId
                })
                .HasPrincipalKey(project => new
                {
                    project.Id,
                    project.TenantId
                })
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(workItem => new
            {
                workItem.TenantId,
                workItem.ProjectId
            })
            .HasDatabaseName("idx_work_items_tenant_project");




            builder.HasIndex(workItem => workItem.ProjectId)
                .HasDatabaseName("idx_work_items_project");

            builder.HasIndex(workItem => workItem.CanonicalConceptId)
                .HasDatabaseName("idx_work_items_concept");                        
        }

        private static MeasurementUnit ResolveMeasurementUnit(
            string code) =>
            code switch
            {
                "M2" => MeasurementUnit.SquareMeter,
                "M3" => MeasurementUnit.CubicMeter,
                "ML" => MeasurementUnit.LinearMeter,
                "UN" => MeasurementUnit.Unit,
                "KG" => MeasurementUnit.Kilogram,
                "TN" => MeasurementUnit.Ton,
                "HR" => MeasurementUnit.Hour,
                "DY" => MeasurementUnit.Day,
                _ => ResolveCompositeUnit(code)
            };

        private static MeasurementUnit ResolveCompositeUnit(
            string code)
        {
            var parts = code.Split(
                '/',
                StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Unknown measurement unit '{code}'.");
            }

            return MeasurementUnit.Composite(
                parts[0],
                parts[1]);
        }
    }
}
