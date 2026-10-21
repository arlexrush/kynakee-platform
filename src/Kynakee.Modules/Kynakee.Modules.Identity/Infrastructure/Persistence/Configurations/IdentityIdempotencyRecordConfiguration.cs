using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityIdempotencyRecordConfiguration : IEntityTypeConfiguration<IdentityIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdentityIdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("idempotency_records", table =>
            table.HasCheckConstraint("ck_identity_idempotency_records_deleted", "is_deleted = (deleted_at IS NOT NULL)"));
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(record => record.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(record => record.KeyHash).HasColumnName("key_hash").HasMaxLength(64).IsRequired();
        builder.Property(record => record.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64).IsRequired();
        builder.Property<byte[]>("_encryptedOutcome")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("encrypted_outcome")
            .HasColumnType("bytea")
            .IsRequired();
        builder.Property(record => record.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(record => record.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(record => record.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(record => record.CreatedBy).HasColumnName("created_by");
        builder.Property(record => record.UpdatedBy).HasColumnName("updated_by");
        builder.Property(record => record.DeletedAt).HasColumnName("deleted_at");
        builder.Property(record => record.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(record => record.Version).HasColumnName("xmin").IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(record => record.KeyHash)
            .IsUnique()
            .HasDatabaseName("ux_identity_idempotency_records_tenant_key");
        builder.HasIndex(record => record.ExpiresAt).HasDatabaseName("ix_identity_idempotency_records_expiry");
    }
}
