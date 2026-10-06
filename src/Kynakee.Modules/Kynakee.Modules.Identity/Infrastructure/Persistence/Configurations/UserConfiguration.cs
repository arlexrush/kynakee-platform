using Kynakee.Modules.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users", table =>
            table.HasCheckConstraint("ck_identity_users_deleted", "is_deleted = (deleted_at IS NOT NULL)"));
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(user => user.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.Property(user => user.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(user => user.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(1024);
        builder.Property(user => user.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.OwnsOne(user => user.Email, email =>
        {
            email.Property(value => value.Value)
                .HasColumnName("normalized_email")
                .HasMaxLength(320)
                .IsRequired();
            email.HasIndex(value => value.Value)
                .IsUnique()
                .HasFilter("\"is_deleted\" = false")
                .HasDatabaseName("ux_identity_users_normalized_email");
        });
        builder.Navigation(user => user.Email).IsRequired();

        builder.OwnsOne(user => user.Phone, phone =>
        {
            phone.Property(value => value.Value)
                .HasColumnName("phone_number")
                .HasMaxLength(16);
        });
        builder.Navigation(user => user.Phone).IsRequired(false);

        builder.Property(user => user.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(user => user.CreatedBy).HasColumnName("created_by");
        builder.Property(user => user.UpdatedBy).HasColumnName("updated_by");
        builder.Property(user => user.DeletedAt).HasColumnName("deleted_at");
        builder.Property(user => user.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(user => user.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(user => new { user.TenantId, user.Status })
            .HasDatabaseName("ix_identity_users_tenant_status");
        builder.HasAlternateKey(user => new { user.TenantId, user.Id })
            .HasName("ak_identity_users_tenant_id");
    }
}