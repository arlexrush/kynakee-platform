using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.ReviewProject;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("reviews");

        builder.HasKey(review => review.Id);

        builder.HasAlternateKey(review => new
        {
            review.Id,
            review.TenantId
        });

        builder.HasAlternateKey(review => new
        {
            review.Id,
            review.ProjectId,
            review.TenantId
        });

        builder.Property(review => review.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new ReviewId(value))
            .ValueGeneratedNever();

        builder.Property(review => review.ProjectId)
            .HasColumnName("project_id")
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(review => review.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(review => review.ReviewerId)
            .HasColumnName("reviewer_id")
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(review => review.IsApproved)
            .HasColumnName("is_approved")
            .IsRequired();

        builder.Property(review => review.ApprovedAt)
            .HasColumnName("approved_at");

        builder.Property(review => review.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(review => review.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(review => review.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(review => review.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(review => review.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(review => review.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(review => review.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(review => new
            {
                review.ProjectId,
                review.TenantId
            })
            .HasPrincipalKey(project => new
            {
                project.Id,
                project.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(
            review => review.AIActLog,
            log =>
            {
                log.Property(value => value.SupervisingHumanId)
                    .HasColumnName("ai_act_supervising_human_id");

                log.Property(value => value.ReviewedAt)
                    .HasColumnName("ai_act_reviewed_at")
                    .IsRequired();

                log.Property(value => value.ItemsReviewed)
                    .HasColumnName("ai_act_items_reviewed")
                    .IsRequired();

                log.Property(value => value.ItemsModified)
                    .HasColumnName("ai_act_items_modified")
                    .IsRequired();

                log.Property(value => value.Article14Confirmed)
                    .HasColumnName("ai_act_article_14_confirmed")
                    .IsRequired();
            });

        builder.Navigation(review => review.AIActLog)
            .IsRequired();

        builder.OwnsMany(
            review => review.Changes,
            change =>
            {
                change.ToTable("review_changes");

                change.Property<ReviewId>("ReviewId")
                    .HasColumnName("review_id")
                    .HasConversion(
                        id => id.Value,
                        value => new ReviewId(value));

                change.Property<Guid>("TenantId")
                    .HasColumnName("tenant_id");

                change.Property<Guid>("EntryId")
                    .HasColumnName("entry_id")
                    .ValueGeneratedOnAdd();

                change.HasKey("ReviewId", "TenantId", "EntryId");

                change.WithOwner()
                    .HasForeignKey("ReviewId", "TenantId")
                    .HasPrincipalKey(
                        nameof(Review.Id),
                        nameof(Review.TenantId));

                change.Property(value => value.Field)
                    .HasColumnName("field")
                    .HasMaxLength(250)
                    .IsRequired();

                change.Property(value => value.PreviousValue)
                    .HasColumnName("previous_value");

                change.Property(value => value.NewValue)
                    .HasColumnName("new_value");

                change.Property(value => value.Reason)
                    .HasColumnName("reason");
            });

        builder.Navigation(review => review.Changes)
            .HasField("_changes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
