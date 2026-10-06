using Kynakee.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tenant_users", table =>
            table.HasCheckConstraint("ck_identity_tenant_users_deleted", "is_deleted = (deleted_at IS NOT NULL)"));
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(membership => membership.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(membership => membership.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(membership => membership.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(membership => membership.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(membership => membership.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(membership => membership.CreatedBy).HasColumnName("created_by");
        builder.Property(membership => membership.UpdatedBy).HasColumnName("updated_by");
        builder.Property(membership => membership.DeletedAt).HasColumnName("deleted_at");
        builder.Property(membership => membership.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(membership => membership.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(membership => membership.UserId)
            .IsUnique()
            .HasDatabaseName("ux_identity_tenant_users_user");
        builder.HasIndex(membership => new { membership.TenantId, membership.Role })
            .HasDatabaseName("ix_identity_tenant_users_tenant_role");
        builder.HasOne<Kynakee.Modules.Identity.Domain.Aggregates.User>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.UserId })
            .HasPrincipalKey(user => new { user.TenantId, user.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}