using Kynakee.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Configures a unique durable handoff per account action token.</summary>
public sealed class AccountEmailJobConfiguration : IEntityTypeConfiguration<AccountEmailJob>
{
    /// <summary>Configures encrypted storage, ownership and pending delivery indexing.</summary>
    public void Configure(EntityTypeBuilder<AccountEmailJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("account_email_jobs", table => table.HasCheckConstraint("ck_identity_account_email_deleted", "is_deleted = (deleted_at IS NOT NULL)"));
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(job => job.TenantId).HasColumnName("tenant_id");
        builder.Property(job => job.TokenId).HasColumnName("token_id");
        builder.Property(job => job.EncryptedPayload).HasColumnName("encrypted_payload").HasColumnType("bytea");
        builder.Property(job => job.ExpiresAt).HasColumnName("expires_at");
        builder.Property(job => job.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(job => job.CompletedAt).HasColumnName("completed_at");
        builder.Property(job => job.Attempts).HasColumnName("attempts");
        builder.Property(job => job.FailureCode).HasColumnName("failure_code").HasMaxLength(64);
        builder.Property(job => job.CreatedAt).HasColumnName("created_at");
        builder.Property(job => job.UpdatedAt).HasColumnName("updated_at");
        builder.Property(job => job.CreatedBy).HasColumnName("created_by");
        builder.Property(job => job.UpdatedBy).HasColumnName("updated_by");
        builder.Property(job => job.IsDeleted).HasColumnName("is_deleted");
        builder.Property(job => job.DeletedAt).HasColumnName("deleted_at");
        builder.Property(job => job.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasIndex(job => job.TokenId).IsUnique().HasDatabaseName("ux_identity_account_email_token");
        builder.HasIndex(job => new { job.NextAttemptAt, job.Id }).HasFilter("completed_at IS NULL AND NOT is_deleted")
            .HasDatabaseName("ix_identity_account_email_pending");
        builder.HasOne<AccountActionToken>().WithMany().HasForeignKey(job => new { job.TenantId, job.TokenId })
            .HasPrincipalKey(token => new { token.TenantId, token.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
