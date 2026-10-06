using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class APUComponentConfiguration :
    IEntityTypeConfiguration<APUComponent>
{
    public void Configure(EntityTypeBuilder<APUComponent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.ToTable("apu_components");

        builder.HasKey(component => component.Id);

        builder.HasAlternateKey(component => new
        {
            component.Id,
            component.TenantId
        })
        .HasName("ak_apu_components_id_tenant_id");

        builder.HasDiscriminator<string>("component_type")
            .HasValue<MaterialComponent>("material")
            .HasValue<LaborComponent>("labor")
            .HasValue<EquipmentComponent>("equipment")
            .HasValue<TransportComponent>("transport")
            .HasValue<SubcontractComponent>("subcontract")
            .HasValue<AuxiliaryMeansComponent>("auxiliary_means");

        builder.Ignore(component => component.Type);
        builder.Ignore(component => component.CurrentPricing);

        builder.Property(component => component.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new APUComponentId(value))
            .ValueGeneratedNever();

        builder.Property(component => component.APUAssignmentId)
            .HasColumnName("apu_assignment_id")
            .HasConversion(
                id => id.Value,
                value => new APUAssignmentId(value))
            .IsRequired();

        builder.Property(component => component.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(component => component.SourceComponentId)
            .HasColumnName("source_component_id")
            .IsRequired();

        builder.Property(component => component.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(component => component.Unit)
            .HasColumnName("unit")
            .HasConversion(
                unit => unit.Code,
                code => ResolveUnit(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(component => component.UnitRevision)
            .HasColumnName("unit_revision")
            .IsRequired();

        builder.Property(component => component.FallbackIndicator)
            .HasColumnName("fallback_indicator");

        builder.Property(component => component.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(component => component.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(component => component.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(component => component.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(component => component.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(component => component.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(component => component.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Navigation(component => component.PricingHistory)
            .HasField("_pricingHistory")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(component => new
        {
            component.TenantId,
            component.APUAssignmentId
        })
        .HasDatabaseName("idx_apu_components_tenant_assignment");
    }

    private static MeasurementUnit ResolveUnit(string code)
    {
        return code switch
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
    }

    private static MeasurementUnit ResolveCompositeUnit(string code)
    {
        var parts = code.Split(
            '/',
            StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                $"Unknown measurement unit '{code}'.");
        }

        return MeasurementUnit.Composite(parts[0], parts[1]);
    }
}
