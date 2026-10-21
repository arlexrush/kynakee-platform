using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Persists action tokens independently from invitation credentials.</summary>
public sealed class AccountActionTokenConfiguration : IEntityTypeConfiguration<AccountActionToken>
{
    /// <summary>Configures tenant ownership, lifecycle constraints and digest uniqueness.</summary>
    public void Configure(EntityTypeBuilder<AccountActionToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("account_action_tokens", table =>
        {
            table.HasCheckConstraint("ck_identity_account_tokens_deleted", "is_deleted = (deleted_at IS NOT NULL)");
            table.HasCheckConstraint("ck_identity_account_tokens_expiry", "expires_at > issued_at");
            table.HasCheckConstraint("ck_identity_account_tokens_terminal", "consumed_at IS NULL OR revoked_at IS NULL");
            table.HasCheckConstraint("ck_identity_account_tokens_purpose", "purpose IN ('VerifyEmail', 'ResetPassword')");
        });
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.TenantId).HasColumnName("tenant_id");
        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.Purpose).HasColumnName("purpose").HasConversion<string>().HasMaxLength(30);
        builder.Property(token => token.IssuedAt).HasColumnName("issued_at");
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        builder.Property(token => token.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(token => token.RevokedAt).HasColumnName("revoked_at");
        builder.Property(token => token.CreatedAt).HasColumnName("created_at");
        builder.Property(token => token.UpdatedAt).HasColumnName("updated_at");
        builder.Property(token => token.CreatedBy).HasColumnName("created_by");
        builder.Property(token => token.UpdatedBy).HasColumnName("updated_by");
        builder.Property(token => token.IsDeleted).HasColumnName("is_deleted");
        builder.Property(token => token.DeletedAt).HasColumnName("deleted_at");
        builder.Property(token => token.Version).HasColumnName("xmin").IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("ux_identity_account_tokens_hash");
        builder.HasIndex(token => new { token.TenantId, token.UserId, token.Purpose, token.IssuedAt })
            .HasDatabaseName("ix_identity_account_tokens_owner_purpose");
        builder.HasOne<User>().WithMany().HasForeignKey(token => new { token.TenantId, token.UserId })
            .HasPrincipalKey(user => new { user.TenantId, user.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
