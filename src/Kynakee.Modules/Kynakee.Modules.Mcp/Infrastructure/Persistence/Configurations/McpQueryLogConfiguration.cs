using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Mcp.Infrastructure.Persistence.Configurations;

public sealed class McpQueryLogConfiguration : IEntityTypeConfiguration<McpQueryLog>
{
    public void Configure(EntityTypeBuilder<McpQueryLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mcp_query_logs", table =>
        {
            table.HasCheckConstraint("ck_mcp_query_logs_deleted", "is_deleted = (deleted_at IS NOT NULL)");
            table.HasCheckConstraint("ck_mcp_query_logs_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_mcp_query_logs_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
            table.HasCheckConstraint("ck_mcp_query_logs_duration", "duration_ms IS NULL OR duration_ms >= 0");
            table.HasCheckConstraint("ck_mcp_query_logs_credits", "credits_charged >= 0");
        });
        builder.HasKey(queryLog => queryLog.Id);
        builder.Property(queryLog => queryLog.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new McpQueryLogId(value))
            .ValueGeneratedNever();
        builder.Property(queryLog => queryLog.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(queryLog => queryLog.ProjectId).HasColumnName("project_id").IsRequired(false);
        builder.Property(queryLog => queryLog.ProviderId)
            .HasColumnName("provider_id")
            .HasConversion(id => id.Value, value => new McpProviderId(value))
            .IsRequired();
        builder.Property(queryLog => queryLog.CanonicalConceptId)
            .HasColumnName("canonical_concept_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(queryLog => queryLog.ComponentType)
            .HasColumnName("component_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(queryLog => queryLog.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(12, 4)
            .IsRequired();
        builder.Property(queryLog => queryLog.Unit).HasColumnName("unit").HasMaxLength(20).IsRequired();
        builder.Property(queryLog => queryLog.ResponsePrice)
            .HasColumnName("response_price")
            .HasPrecision(12, 4);
        builder.Property(queryLog => queryLog.ResponseUnit)
            .HasColumnName("response_unit")
            .HasMaxLength(20);
        builder.Property(queryLog => queryLog.FallbackSource)
            .HasColumnName("fallback_source")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Ignore(queryLog => queryLog.FallbackActivated);
        builder.Property(queryLog => queryLog.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(4, 3);
        builder.Property(queryLog => queryLog.DurationMilliseconds)
            .HasColumnName("duration_ms");
        builder.Property(queryLog => queryLog.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(queryLog => queryLog.CreditsCharged)
            .HasColumnName("credits_charged")
            .HasPrecision(10, 4)
            .IsRequired();
        builder.Property(queryLog => queryLog.QueriedAt).HasColumnName("queried_at").IsRequired();

        builder.Property(queryLog => queryLog.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(queryLog => queryLog.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(queryLog => queryLog.CreatedBy).HasColumnName("created_by");
        builder.Property(queryLog => queryLog.UpdatedBy).HasColumnName("updated_by");
        builder.Property(queryLog => queryLog.DeletedAt).HasColumnName("deleted_at");
        builder.Property(queryLog => queryLog.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Ignore(queryLog => queryLog.Version);

        builder.HasOne<McpProvider>()
            .WithMany()
            .HasForeignKey(queryLog => queryLog.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(queryLog => new { queryLog.TenantId, queryLog.ProjectId, queryLog.QueriedAt })
            .HasDatabaseName("idx_mcp_query_logs_project");
        builder.HasIndex(queryLog => new { queryLog.TenantId, queryLog.ProviderId, queryLog.QueriedAt })
            .HasDatabaseName("idx_mcp_query_logs_provider");
    }
}
