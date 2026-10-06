using Kynakee.Modules.Ai.Domain.Entities;
using Kynakee.Modules.Ai.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Ai.Infrastructure.Persistence.Configurations;

public sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("agent_runs");
        builder.HasKey(run => run.Id);

        builder.Property(run => run.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new AgentRunId(value))
            .ValueGeneratedNever();

        builder.Property(run => run.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.Property(run => run.ProjectId)
            .HasColumnName("project_id");
        builder.Property(run => run.AgentType)
            .HasColumnName("agent_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(run => run.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(run => run.Provider)
            .HasColumnName("provider")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(run => run.InputTokens)
            .HasColumnName("input_tokens")
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(run => run.OutputTokens)
            .HasColumnName("output_tokens")
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(run => run.CreditsCharged)
            .HasColumnName("credits_charged")
            .HasPrecision(10, 4)
            .HasDefaultValue(0m)
            .IsRequired();
        builder.Property(run => run.FallbackActivated)
            .HasColumnName("fallback_activated")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(run => run.FallbackReason)
            .HasColumnName("fallback_reason")
            .HasMaxLength(200);
        builder.Property(run => run.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(run => run.Duration)
            .HasColumnName("duration_ms")
            .HasConversion(
                duration => checked((int)duration.TotalMilliseconds),
                milliseconds => TimeSpan.FromMilliseconds(milliseconds));
        builder.Property(run => run.HumanReviewed)
            .HasColumnName("human_reviewed")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(run => run.HumanReviewedAt)
            .HasColumnName("human_reviewed_at");
        builder.Property(run => run.HumanReviewerId)
            .HasColumnName("human_reviewer_id");

        builder.Property(run => run.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(run => run.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
        builder.Property(run => run.CreatedBy)
            .HasColumnName("created_by");
        builder.Property(run => run.UpdatedBy)
            .HasColumnName("updated_by");
        builder.Property(run => run.DeletedAt)
            .HasColumnName("deleted_at");
        builder.Property(run => run.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();
        builder.Property(run => run.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(run => new { run.ProjectId, run.CreatedAt })
            .HasDatabaseName("idx_agent_runs_project");
        builder.HasIndex(run => new { run.TenantId, run.CreatedAt })
            .HasDatabaseName("idx_agent_runs_tenant");
    }
}
