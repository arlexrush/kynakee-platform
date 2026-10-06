using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class APUComponentPricingConfiguration :
    IEntityTypeConfiguration<APUComponentPricing>
{
    public void Configure(EntityTypeBuilder<APUComponentPricing> builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.ToTable("apu_component_pricing");

        builder.HasKey(pricing => pricing.Id);

        builder.Property(pricing => pricing.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new APUComponentPricingId(value))
            .ValueGeneratedNever();

        builder.Property(pricing => pricing.APUComponentId)
            .HasColumnName("apu_component_id")
            .HasConversion(
                id => id.Value,
                value => new APUComponentId(value))
            .IsRequired();

        builder.Property(pricing => pricing.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(pricing => pricing.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.OwnsOne(
            pricing => pricing.UnitPrice,
            money =>
            {
                money.Property(value => value.Amount)
                    .HasColumnName("unit_price_amount")
                    .HasPrecision(18, 6);

                money.Property(value => value.Currency)
                    .HasColumnName("unit_price_currency")
                    .HasMaxLength(3);
            });

        builder.Property(pricing => pricing.QuotedUnit)
            .HasColumnName("quoted_unit")
            .HasConversion(
                unit => unit == null ? null : unit.Code,
                code => code == null ? null : ResolveQuotedUnit(code))
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(pricing => pricing.UnitRevision)
            .HasColumnName("unit_revision")
            .IsRequired(false);

        builder.Property(pricing => pricing.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(200);

        builder.Property(pricing => pricing.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(pricing => pricing.QueriedAt)
            .HasColumnName("queried_at")
            .IsRequired();

        builder.Property(pricing => pricing.Confidence)
            .HasColumnName("confidence")
            .HasConversion(
                confidence => confidence.Value,
                value => new Confidence(value))
            .HasPrecision(4, 3)
            .IsRequired();

        builder.Property(pricing => pricing.IsFallback)
            .HasColumnName("is_fallback")
            .IsRequired();

        builder.Property(pricing => pricing.FailureReason)
            .HasColumnName("failure_reason");

        builder.Ignore(pricing => pricing.HasPrice);

        builder.Property(pricing => pricing.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(pricing => pricing.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(pricing => pricing.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(pricing => pricing.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(pricing => pricing.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(pricing => pricing.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(pricing => pricing.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<APUComponent>()
            .WithMany(component => component.PricingHistory)
            .HasForeignKey(pricing => new
            {
                pricing.APUComponentId,
                pricing.TenantId
            })
            .HasPrincipalKey(component => new
            {
                component.Id,
                component.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pricing => new
        {
            pricing.TenantId,
            pricing.APUComponentId,
            pricing.QueriedAt
        })
        .HasDatabaseName("idx_apu_component_pricing_history");
    }

    private static MeasurementUnit ResolveQuotedUnit(string code) =>
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

        if (parts.Length != 2 ||
            parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"Unknown quoted measurement unit '{code}'.");
        }

        return MeasurementUnit.Composite(parts[0], parts[1]);
    }

}
