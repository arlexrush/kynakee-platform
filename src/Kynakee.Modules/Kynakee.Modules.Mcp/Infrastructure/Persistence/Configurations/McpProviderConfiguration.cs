using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kynakee.Modules.Mcp.Infrastructure.Persistence.Configurations;

public sealed class McpProviderConfiguration : IEntityTypeConfiguration<McpProvider>
{
    public void Configure(EntityTypeBuilder<McpProvider> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mcp_providers", table =>
        {
            table.HasCheckConstraint("ck_mcp_providers_deleted", "is_deleted = (deleted_at IS NOT NULL)");
            table.HasCheckConstraint("ck_mcp_providers_rating", "rating >= 0 AND rating <= 5");
            table.HasCheckConstraint("ck_mcp_providers_failures", "consecutive_failures >= 0");
        });
        builder.HasKey(provider => provider.Id);
        builder.Property(provider => provider.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new McpProviderId(value))
            .ValueGeneratedNever();
        builder.Property(provider => provider.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.HasAlternateKey(provider => new { provider.TenantId, provider.Id })
            .HasName("ak_mcp_providers_tenant_id_id");

        builder.Property(provider => provider.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Ignore(provider => provider.Categories);
        builder.Property<List<string>>("_categories")
            .HasField("_categories")
            .HasColumnName("categories")
            .HasColumnType("text[]")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(provider => provider.GeoRegions);
        builder.Property<List<string>>("_geoRegions")
            .HasField("_geoRegions")
            .HasColumnName("geo_regions")
            .HasColumnType("text[]")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(provider => provider.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(provider => provider.Rating)
            .HasColumnName("rating")
            .HasConversion(new ValueConverter<ProviderRating, decimal>(
                rating => rating.Value,
                value => ProviderRating.Create(value).Value))
            .HasPrecision(3, 2)
            .IsRequired();
        builder.Property(provider => provider.ConsecutiveFailures)
            .HasColumnName("consecutive_failures")
            .IsRequired();
        builder.Property(provider => provider.LastSuccessAt)
            .HasColumnName("last_success_at");
        builder.Property(provider => provider.SuspendedUntil)
            .HasColumnName("suspended_until");

        builder.Property(provider => provider.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(provider => provider.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(provider => provider.CreatedBy).HasColumnName("created_by");
        builder.Property(provider => provider.UpdatedBy).HasColumnName("updated_by");
        builder.Property(provider => provider.DeletedAt).HasColumnName("deleted_at");
        builder.Property(provider => provider.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(provider => provider.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex("_geoRegions").HasMethod("gin").HasDatabaseName("idx_mcp_providers_region");
        builder.HasIndex("_categories").HasMethod("gin").HasDatabaseName("idx_mcp_providers_category");
        builder.HasIndex(provider => new { provider.TenantId, provider.Status })
            .HasDatabaseName("idx_mcp_providers_tenant_status");

        builder.HasMany(provider => provider.Servers)
            .WithOne()
            .HasForeignKey(server => new { server.TenantId, server.ProviderId })
            .HasPrincipalKey(provider => new { provider.TenantId, provider.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(provider => provider.Servers)
            .HasField("_servers")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
