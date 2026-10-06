using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class ValuationConfiguration :
    IEntityTypeConfiguration<Valuation>
{
    public void Configure(EntityTypeBuilder<Valuation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.ToTable("valuations");

        builder.HasKey(valuation => valuation.Id);

        builder.HasAlternateKey(valuation => new
        {
            valuation.Id,
            valuation.ProjectId,
            valuation.TenantId
        })
        .HasName("ak_valuations_id_project_tenant");

        builder.Property(valuation => valuation.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new ValuationId(value))
            .ValueGeneratedNever();

        builder.Property(valuation => valuation.ProjectId)
            .HasColumnName("project_id")
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(valuation => valuation.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(valuation => valuation.ConfidenceLevel)
            .HasColumnName("confidence_level")
            .HasConversion(
                confidence => confidence.Value,
                value => new Confidence(value))
            .HasPrecision(4, 3)
            .IsRequired();

        builder.Property(valuation => valuation.ValuatedAt)
            .HasColumnName("valuated_at")
            .IsRequired();

        ConfigureMoney(builder);

        builder.Ignore(valuation => valuation.ValuedComponents);
        builder.Ignore(valuation => valuation.ValuedWorkItems);
        builder.Ignore(valuation => valuation.IsComplete);

        builder.Property<List<ValuedComponent>>("_valuedComponents")
            .HasColumnName("valued_components")
            .HasColumnType("jsonb")
            .HasConversion(
                components =>
                    ValuationSnapshotJson.SerializeComponents(components),
                json =>
                    ValuationSnapshotJson.DeserializeComponents(json))
            .Metadata.SetValueComparer(
                CreateComparer<ValuedComponent>(
                    ValuationSnapshotJson.SerializeComponents));

        builder.Property<List<ValuedWorkItem>>("_valuedWorkItems")
            .HasColumnName("valued_work_items")
            .HasColumnType("jsonb")
            .HasConversion(
                items =>
                    ValuationSnapshotJson.SerializeWorkItems(items),
                json =>
                    ValuationSnapshotJson.DeserializeWorkItems(json))
            .Metadata.SetValueComparer(
                CreateComparer<ValuedWorkItem>(
                    ValuationSnapshotJson.SerializeWorkItems));

        builder.Property(valuation => valuation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(valuation => valuation.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(valuation => valuation.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(valuation => valuation.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(valuation => valuation.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(valuation => valuation.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(valuation => valuation.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(valuation => new
            {
                valuation.ProjectId,
                valuation.TenantId
            })
            .HasPrincipalKey(project => new
            {
                project.Id,
                project.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(valuation => new
        {
            valuation.TenantId,
            valuation.ProjectId
        })
        .HasDatabaseName("idx_valuations_tenant_project");
    }

    private static void ConfigureMoney(
        EntityTypeBuilder<Valuation> builder)
    {
        builder.OwnsOne(
            valuation => valuation.DirectCost,
            money => MapMoney(money, "direct_cost"));

        builder.OwnsOne(
            valuation => valuation.AuxiliaryCost,
            money => MapMoney(money, "auxiliary_cost"));

        builder.OwnsOne(
            valuation => valuation.IndirectCost,
            money => MapMoney(money, "indirect_cost"));

        builder.OwnsOne(
            valuation => valuation.Administration,
            money => MapMoney(money, "administration"));

        builder.OwnsOne(
            valuation => valuation.Quality,
            money => MapMoney(money, "quality"));

        builder.OwnsOne(
            valuation => valuation.SafetyHealth,
            money => MapMoney(money, "safety_health"));

        builder.OwnsOne(
            valuation => valuation.Environment,
            money => MapMoney(money, "environment"));

        builder.OwnsOne(
            valuation => valuation.Contingency,
            money => MapMoney(money, "contingency"));

        builder.OwnsOne(
            valuation => valuation.Profit,
            money => MapMoney(money, "profit"));

        builder.OwnsOne(
            valuation => valuation.VAT,
            money => MapMoney(money, "vat"));

        builder.OwnsOne(
            valuation => valuation.TotalCost,
            money => MapMoney(money, "total_cost"));
    }

    private static void MapMoney(
        OwnedNavigationBuilder<Valuation, Money> money,
        string columnPrefix)
    {
        money.Property(value => value.Amount)
            .HasColumnName($"{columnPrefix}_amount")
            .HasPrecision(18, 6);

        money.Property(value => value.Currency)
            .HasColumnName($"{columnPrefix}_currency")
            .HasMaxLength(3);
    }

    private static ValueComparer<List<T>> CreateComparer<T>(
        Func<List<T>, string> serialize) =>
        new(
            (left, right) =>
                ReferenceEquals(left, right) ||
                left != null &&
                right != null &&
                serialize(left) == serialize(right),
            value => serialize(value).GetHashCode(),
            value => value.ToList());
}