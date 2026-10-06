using Kynakee.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("refresh_tokens", table =>
            table.HasCheckConstraint("ck_identity_refresh_tokens_deleted", "is_deleted = (deleted_at IS NOT NULL)"));
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(token => token.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(token => token.RevokedAt).HasColumnName("revoked_at");
        builder.Property(token => token.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
        builder.Property(token => token.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(token => token.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(token => token.CreatedBy).HasColumnName("created_by");
        builder.Property(token => token.UpdatedBy).HasColumnName("updated_by");
        builder.Property(token => token.DeletedAt).HasColumnName("deleted_at");
        builder.Property(token => token.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(token => token.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_identity_refresh_tokens_hash");
        builder.HasIndex(token => new { token.TenantId, token.UserId, token.ExpiresAt })
            .HasDatabaseName("ix_identity_refresh_tokens_owner_expiry");
    }
}