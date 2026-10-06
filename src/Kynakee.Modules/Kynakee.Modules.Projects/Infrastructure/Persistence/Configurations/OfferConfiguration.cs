using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.OfferProject;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("offers");

        builder.HasKey(offer => offer.Id);

        builder.HasAlternateKey(offer => new
        {
            offer.Id,
            offer.ProjectId,
            offer.TenantId
        });

        builder.Property(offer => offer.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new OfferId(value))
            .ValueGeneratedNever();

        builder.Property(offer => offer.ProjectId)
            .HasColumnName("project_id")
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(offer => offer.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(offer => offer.OfferNumber)
            .HasColumnName("offer_number")
            .IsRequired();

        builder.OwnsOne(
            offer => offer.TotalAmount,
            money =>
            {
                money.Property(value => value.Amount)
                    .HasColumnName("total_amount")
                    .HasPrecision(18, 6)
                    .IsRequired();

                money.Property(value => value.Currency)
                    .HasColumnName("total_currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

        builder.Navigation(offer => offer.TotalAmount)
            .IsRequired();

        builder.Property(offer => offer.Conditions)
            .HasColumnName("conditions");

        builder.Property(offer => offer.Warranties)
            .HasColumnName("warranties");

        builder.Property(offer => offer.ValidityDays)
            .HasColumnName("validity_days")
            .IsRequired();

        builder.Property(offer => offer.PdfUrl)
            .HasColumnName("pdf_url")
            .HasConversion(
                uri => uri == null ? null : uri.OriginalString,
                value => value == null
                    ? null
                    : new Uri(value, UriKind.RelativeOrAbsolute))
            .HasMaxLength(2048);

        builder.Property(offer => offer.SentAt)
            .HasColumnName("sent_at");

        builder.Property(offer => offer.AIActDisclaimer)
            .HasColumnName("ai_act_disclaimer");

        builder.Property(offer => offer.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(offer => offer.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(offer => offer.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(offer => offer.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(offer => offer.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(offer => offer.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(offer => offer.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(offer => offer.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(offer => new
            {
                offer.ProjectId,
                offer.TenantId
            })
            .HasPrincipalKey(project => new
            {
                project.Id,
                project.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(offer => new
        {
            offer.TenantId,
            offer.ProjectId,
            offer.OfferNumber
        });
    }
}
