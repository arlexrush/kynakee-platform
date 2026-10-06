using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class APUAssignmentConfiguration :
    IEntityTypeConfiguration<APUAssignment>
{
    public void Configure(EntityTypeBuilder<APUAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.ToTable("apu_assignments");

        builder.HasKey(assignment => assignment.Id);

        builder.HasAlternateKey(assignment => new
        {
            assignment.Id,
            assignment.TenantId
        })
        .HasName("ak_apu_assignments_id_tenant_id");

        builder.Property(assignment => assignment.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new APUAssignmentId(value))
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.ProjectId)
            .HasColumnName("project_id")
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(assignment => assignment.WorkItemId)
            .HasColumnName("work_item_id")
            .HasConversion(
                id => id.Value,
                value => new WorkItemId(value))
            .IsRequired();

        builder.Property(assignment => assignment.OutputUnit)
            .HasColumnName("output_unit")
            .HasConversion(
                unit => unit.Code,
                code => ResolveOutputUnit(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(assignment => assignment.APUTemplateId)
            .HasColumnName("apu_template_id")
            .HasConversion(
                id => id.Value,
                value => new APUTemplateId(value))
            .IsRequired();

        builder.Property(assignment => assignment.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(assignment => assignment.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(assignment => assignment.Confidence)
            .HasColumnName("confidence")
            .HasConversion(
                confidence => confidence.Value,
                value => new Confidence(value))
            .HasPrecision(4, 3)
            .IsRequired();

        builder.Property(assignment => assignment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(assignment => assignment.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(assignment => assignment.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(assignment => assignment.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(assignment => assignment.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(assignment => assignment.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(assignment => assignment.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany(project => project.APUAssignments)
            .HasForeignKey(assignment => new
            {
                assignment.ProjectId,
                assignment.TenantId
            })
            .HasPrincipalKey(project => new
            {
                project.Id,
                project.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.WorkItemId,
                assignment.ProjectId,
                assignment.TenantId
            })
            .HasPrincipalKey(workItem => new
            {
                workItem.Id,
                workItem.ProjectId,
                workItem.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(assignment => assignment.Components)
            .WithOne()
            .HasForeignKey(component => new
            {
                component.APUAssignmentId,
                component.TenantId
            })
            .HasPrincipalKey(assignment => new
            {
                assignment.Id,
                assignment.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(assignment => assignment.Components)
            .HasField("_components")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(assignment => new
        {
            assignment.TenantId,
            assignment.ProjectId
        })
        .HasDatabaseName("idx_apu_assignments_tenant_project");

        builder.HasIndex(assignment => new
        {
            assignment.TenantId,
            assignment.ProjectId,
            assignment.WorkItemId
        })
        .IsUnique()
        .HasFilter("is_deleted = false")
        .HasDatabaseName("ux_apu_assignments_active_work_item");
    }

    private static MeasurementUnit ResolveOutputUnit(string code) =>
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

    private static MeasurementUnit ResolveCompositeUnit(string code)
    {
        var parts = code.Split('/', StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                $"Unknown measurement unit '{code}'.");
        }

        return MeasurementUnit.Composite(parts[0], parts[1]);
    }

}
