using Kynakee.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_invitations", table =>
            table.HasCheckConstraint("ck_identity_user_invitations_deleted", "is_deleted = (deleted_at IS NOT NULL)"));
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(invitation => invitation.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(invitation => invitation.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(invitation => invitation.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(invitation => invitation.AcceptedAt).HasColumnName("accepted_at");
        builder.Property(invitation => invitation.RevokedAt).HasColumnName("revoked_at");
        builder.Property(invitation => invitation.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(invitation => invitation.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(invitation => invitation.CreatedBy).HasColumnName("created_by");
        builder.Property(invitation => invitation.UpdatedBy).HasColumnName("updated_by");
        builder.Property(invitation => invitation.DeletedAt).HasColumnName("deleted_at");
        builder.Property(invitation => invitation.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(invitation => invitation.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_identity_invitations_hash");
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.UserId, invitation.ExpiresAt })
            .HasDatabaseName("ix_identity_invitations_owner_expiry");
    }
}