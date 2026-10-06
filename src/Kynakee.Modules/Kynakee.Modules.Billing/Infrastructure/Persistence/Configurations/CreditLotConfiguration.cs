using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Billing.Infrastructure.Persistence.Configurations;

public sealed class CreditLotConfiguration : IEntityTypeConfiguration<CreditLot>
{
    public void Configure(EntityTypeBuilder<CreditLot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("credit_lots", table =>
        {
            table.HasCheckConstraint(
                "ck_billing_credit_lots_balance",
                "amount >= 0 AND available_credits >= 0 AND reserved_credits >= 0 AND amount = available_credits + reserved_credits");
            table.HasCheckConstraint(
                "ck_billing_credit_lots_expiry",
                "expires_at >= purchased_at + interval '3 months'");
            table.HasCheckConstraint(
                "ck_billing_credit_lots_deleted",
                "is_deleted = (deleted_at IS NOT NULL)");
        });
        builder.HasKey(lot => lot.Id);
        builder.Property(lot => lot.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new CreditLotId(value))
            .ValueGeneratedNever();
        builder.Property(lot => lot.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(lot => lot.RemainingCredits)
            .HasColumnName("amount")
            .HasPrecision(18, 4)
            .IsRequired();
        builder.Property(lot => lot.AvailableCredits)
            .HasColumnName("available_credits")
            .HasPrecision(18, 4)
            .IsRequired();
        builder.Property(lot => lot.ReservedCredits)
            .HasColumnName("reserved_credits")
            .HasPrecision(18, 4)
            .IsRequired();
        builder.Property(lot => lot.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(lot => lot.PurchasedAt).HasColumnName("purchased_at").IsRequired();
        builder.Property(lot => lot.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(lot => lot.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(lot => lot.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(lot => lot.CreatedBy).HasColumnName("created_by");
        builder.Property(lot => lot.UpdatedBy).HasColumnName("updated_by");
        builder.Property(lot => lot.DeletedAt).HasColumnName("deleted_at");
        builder.Property(lot => lot.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Ignore(nameof(BaseEntity<object>.Version));

        builder.HasIndex(lot => new { lot.TenantId, lot.ExpiresAt, lot.PurchasedAt })
            .HasDatabaseName("ix_billing_credit_lots_tenant_expiry");
    }
}