using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Billing.Infrastructure.Persistence.Configurations;

public sealed class CreditTransactionConfiguration : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("credit_transactions", table =>
        {
            table.HasCheckConstraint(
                "ck_billing_credit_transactions_amount",
                "amount > 0");
            table.HasCheckConstraint(
                "ck_billing_credit_transactions_operation",
                "type = 'Recharged' OR operation_id IS NOT NULL");
            table.HasCheckConstraint(
                "ck_billing_credit_transactions_deleted",
                "is_deleted = (deleted_at IS NOT NULL)");
        });
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(transaction => transaction.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.Property(transaction => transaction.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(transaction => transaction.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 4)
            .IsRequired();
        builder.Property(transaction => transaction.OperationId).HasColumnName("operation_id");
        builder.Property(transaction => transaction.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(transaction => transaction.ExpiresAt).HasColumnName("expires_at");
        builder.Property(transaction => transaction.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(transaction => transaction.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(transaction => transaction.CreatedBy).HasColumnName("created_by");
        builder.Property(transaction => transaction.UpdatedBy).HasColumnName("updated_by");
        builder.Property(transaction => transaction.DeletedAt).HasColumnName("deleted_at");
        builder.Property(transaction => transaction.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Ignore(nameof(BaseEntity<object>.Version));

        builder.OwnsMany(transaction => transaction.Allocations, allocation =>
        {
            allocation.ToTable("credit_transaction_allocations");
            allocation.Property<int>("id")
                .HasColumnName("id")
                .ValueGeneratedOnAdd();
            allocation.HasKey("id");
            allocation.WithOwner()
                .HasForeignKey("credit_transaction_id");
            allocation.Property<Guid>("credit_transaction_id")
                .HasColumnName("credit_transaction_id")
                .IsRequired();
            allocation.Property(item => item.CreditLotId)
                .HasConversion(id => id.Value, value => new CreditLotId(value))
                .HasColumnName("credit_lot_id")
                .IsRequired();
            allocation.Property(item => item.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 4)
                .IsRequired();
        });
        builder.Navigation(transaction => transaction.Allocations)
            .HasField("_allocations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(transaction => new
            {
                transaction.TenantId,
                transaction.CreatedAt,
                transaction.Id
            })
            .HasDatabaseName("ix_billing_credit_transactions_history");
        builder.HasIndex(transaction => new
            {
                transaction.TenantId,
                transaction.OperationId,
                transaction.CreatedAt
            })
            .HasDatabaseName("ix_billing_credit_transactions_operation");
    }
}