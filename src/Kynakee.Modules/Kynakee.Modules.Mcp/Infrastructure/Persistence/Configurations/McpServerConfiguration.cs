using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kynakee.Modules.Mcp.Infrastructure.Persistence.Configurations;

public sealed class McpServerConfiguration : IEntityTypeConfiguration<McpServer>
{
    public void Configure(EntityTypeBuilder<McpServer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mcp_servers", table =>
        {
            table.HasCheckConstraint("ck_mcp_servers_deleted", "is_deleted = (deleted_at IS NOT NULL)");
            table.HasCheckConstraint("ck_mcp_servers_endpoint_https", "endpoint LIKE 'https://%'");
        });
        builder.HasKey(server => server.Id);
        builder.Property(server => server.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new McpServerId(value))
            .ValueGeneratedNever();
        builder.Property(server => server.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.Property(server => server.ProviderId)
            .HasColumnName("provider_id")
            .HasConversion(id => id.Value, value => new McpProviderId(value))
            .IsRequired();
        builder.Property(server => server.Endpoint)
            .HasColumnName("endpoint")
            .HasConversion(new ValueConverter<Uri, string>(
                endpoint => endpoint.ToString(),
                value => new Uri(value, UriKind.Absolute)))
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(server => server.CredentialSecretReference)
            .HasColumnName("credential_secret_reference")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(server => server.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(server => server.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(server => server.CreatedBy).HasColumnName("created_by");
        builder.Property(server => server.UpdatedBy).HasColumnName("updated_by");
        builder.Property(server => server.DeletedAt).HasColumnName("deleted_at");
        builder.Property(server => server.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Ignore(server => server.Version);

        builder.HasIndex(server => server.Endpoint)
            .IsUnique()
            .HasFilter("is_deleted = FALSE")
            .HasDatabaseName("idx_mcp_servers_endpoint");
        builder.HasIndex(server => new { server.TenantId, server.ProviderId })
            .HasDatabaseName("idx_mcp_servers_provider");
    }
}
