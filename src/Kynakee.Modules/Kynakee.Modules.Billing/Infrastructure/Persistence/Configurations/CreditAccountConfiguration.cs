using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Billing.Infrastructure.Persistence.Configurations;

public sealed class CreditAccountConfiguration : IEntityTypeConfiguration<CreditAccount>
{
    public void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("credit_accounts", table =>
        {
            table.HasCheckConstraint(
                "ck_billing_credit_accounts_deleted",
                "is_deleted = (deleted_at IS NOT NULL)");
        });
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new CreditAccountId(value))
            .ValueGeneratedNever();
        builder.Property(account => account.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.HasAlternateKey(account => account.TenantId)
            .HasName("ak_billing_credit_accounts_tenant_id");
        builder.Property(account => account.PlanId)
            .HasColumnName("plan_id")
            .HasConversion(planId => planId.Value, value => new PlanId(value))
            .HasMaxLength(100)
            .IsRequired();

        builder.Ignore(account => account.AvailableCredits);
        builder.Ignore(account => account.ReservedCredits);
        builder.HasMany(account => account.CreditLots)
            .WithOne()
            .HasForeignKey(lot => lot.TenantId)
            .HasPrincipalKey(account => account.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(account => account.CreditLots)
            .HasField("_creditLots")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(account => account.Transactions)
            .WithOne()
            .HasForeignKey(transaction => transaction.TenantId)
            .HasPrincipalKey(account => account.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(account => account.Transactions)
            .HasField("_transactions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(account => account.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(account => account.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(account => account.CreatedBy).HasColumnName("created_by");
        builder.Property(account => account.UpdatedBy).HasColumnName("updated_by");
        builder.Property(account => account.DeletedAt).HasColumnName("deleted_at");
        builder.Property(account => account.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(account => account.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}