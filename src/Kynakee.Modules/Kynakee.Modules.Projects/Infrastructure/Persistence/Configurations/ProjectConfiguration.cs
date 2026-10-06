using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.OfferProject;
using Kynakee.Modules.Projects.Domain.Entities.Planning;
using Kynakee.Modules.Projects.Domain.Entities.ReviewProject;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations
{
    public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
    {        
        public void Configure(
            EntityTypeBuilder<Project> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("projects");

            builder.HasKey(project => project.Id);

            builder.HasAlternateKey(project => new
            {
                project.Id,
                project.TenantId
            })
            .HasName("ak_projects_id_tenant_id");

            builder.Property(project => project.Id)
                .HasColumnName("id")
                .HasConversion(
                    id => id.Value,
                    value => new ProjectId(value))
                .ValueGeneratedNever();

            builder.Property(project => project.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();

            builder.Property(project => project.Name)
                .HasColumnName("name")
                .HasMaxLength(300)
                .IsRequired();

            builder.Property(project => project.Channel)
                .HasColumnName("channel")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(project => project.CurrentPhase)
                .HasColumnName("current_phase")
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(project => project.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property<ValuationId?>("CurrentValuationId")
                .HasColumnName("current_valuation_id")
                .HasConversion(
                    id => id.HasValue ? id.Value.Value : (Guid?)null,
                    value => value.HasValue
                        ? new ValuationId(value.Value)
                        : null);

            builder.HasOne(project => project.Valuation)
                .WithMany()
                .HasForeignKey(
                    "CurrentValuationId",
                    nameof(Project.Id),
                    nameof(Project.TenantId))
                .HasPrincipalKey(
                    nameof(Valuation.Id),
                    nameof(Valuation.ProjectId),
                    nameof(Valuation.TenantId))
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<ScheduleId?>("CurrentScheduleId")
                .HasColumnName("current_schedule_id")
                .HasConversion(
                    id => id.HasValue ? id.Value.Value : (Guid?)null,
                    value => value.HasValue
                        ? new ScheduleId(value.Value)
                        : null);

            builder.HasOne(project => project.Schedule)
                .WithMany()
                .HasForeignKey(
                    "CurrentScheduleId",
                    nameof(Project.Id),
                    nameof(Project.TenantId))
                .HasPrincipalKey(
                    nameof(Schedule.Id),
                    nameof(Schedule.ProjectId),
                    nameof(Schedule.TenantId))
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<ReviewId?>("CurrentReviewId")
                .HasColumnName("current_review_id")
                .HasConversion(
                    id => id.HasValue ? id.Value.Value : (Guid?)null,
                    value => value.HasValue
                        ? new ReviewId(value.Value)
                        : null);

            builder.HasOne(project => project.Review)
                .WithMany()
                .HasForeignKey(
                    "CurrentReviewId",
                    nameof(Project.Id),
                    nameof(Project.TenantId))
                .HasPrincipalKey(
                    nameof(Review.Id),
                    nameof(Review.ProjectId),
                    nameof(Review.TenantId))
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<OfferId?>("CurrentOfferId")
                .HasColumnName("current_offer_id")
                .HasConversion(
                    id => id.HasValue ? id.Value.Value : (Guid?)null,
                    value => value.HasValue
                        ? new OfferId(value.Value)
                        : null);

            builder.HasOne(project => project.Offer)
                .WithMany()
                .HasForeignKey(
                    "CurrentOfferId",
                    nameof(Project.Id),
                    nameof(Project.TenantId))
                .HasPrincipalKey(
                    nameof(Offer.Id),
                    nameof(Offer.ProjectId),
                    nameof(Offer.TenantId))
                .OnDelete(DeleteBehavior.Restrict);


            builder.Property(project => project.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(project => project.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            builder.Property(project => project.CreatedBy)
                .HasColumnName("created_by");

            builder.Property(project => project.UpdatedBy)
                .HasColumnName("updated_by");

            builder.Property(project => project.DeletedAt)
                .HasColumnName("deleted_at");

            builder.Property(project => project.IsDeleted)
                .HasColumnName("is_deleted")
                .IsRequired();

            builder.Property(project => project.Version)
                .HasColumnName("xmin")
                .IsRowVersion()
                .IsConcurrencyToken();

            builder.Navigation(project => project.APUAssignments)
                .HasField("_apuAssignments")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            ConfigureClient(builder);

            ConfigureLocation(builder);

            ConfigureTokenConsumption(builder);

                        
        }

        private static void ConfigureClient(
        EntityTypeBuilder<Project> builder)
        {
            builder.OwnsOne(
                project => project.Client,
                client =>
                {
                    client.Property(value => value.Name)
                        .HasColumnName("client_name")
                        .HasMaxLength(200)
                        .IsRequired();

                    client.Property(value => value.PersonalIdentifier)
                        .HasColumnName("client_personal_identifier")
                        .HasMaxLength(20);

                    client.Property(value => value.BusinessIdentifier)
                        .HasColumnName("client_business_identifier")
                        .HasMaxLength(20);

                    client.Property(value => value.Kind)
                        .HasColumnName("client_kind")
                        .HasConversion<string>()
                        .HasMaxLength(20);

                    client.Property(value => value.TaxCountry)
                        .HasColumnName("client_tax_country")
                        .HasMaxLength(2);

                    client.Property(value => value.Email)
                        .HasColumnName("client_email")
                        .HasMaxLength(200);

                    client.Property(value => value.Phone)
                        .HasColumnName("client_phone")
                        .HasMaxLength(30);
                });
        }

        private static void ConfigureLocation(
        EntityTypeBuilder<Project> builder)
        {
            builder.OwnsOne(
                project => project.Location,
                location =>
                {
                    location.Property(value => value.Country)
                        .HasColumnName("country")
                        .HasMaxLength(2)
                        .IsRequired();

                    location.Property(value => value.Region)
                        .HasColumnName("region")
                        .HasMaxLength(10);

                    location.Property(value => value.Province)
                        .HasColumnName("province")
                        .HasMaxLength(100);

                    location.Property(value => value.Municipality)
                        .HasColumnName("municipality")
                        .HasMaxLength(100);

                    location.Property(value => value.PostalCode)
                        .HasColumnName("postal_code")
                        .HasMaxLength(10);

                    location.Property(value => value.Address)
                        .HasColumnName("address")
                        .HasMaxLength(300);

                    location.Property(value => value.Latitude)
                        .HasColumnName("latitude")
                        .HasPrecision(9, 6);

                    location.Property(value => value.Longitude)
                        .HasColumnName("longitude")
                        .HasPrecision(9, 6);
                });
        }

        private static readonly JsonSerializerOptions TokenJsonOptions =
    new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

        private static void ConfigureTokenConsumption(
            EntityTypeBuilder<Project> builder)
        {
            builder.OwnsOne(
                project => project.TotalTokensConsumed,
                consumption =>
                {
                    consumption.Property(value => value.TotalTokens)
                        .HasColumnName("total_tokens")
                        .IsRequired();

                    consumption.Property(value => value.TotalCredits)
                        .HasColumnName("total_credits")
                        .HasPrecision(10, 4)
                        .IsRequired();

                    consumption.Property(value => value.HasCompletePhaseBreakdown)
                        .HasColumnName("token_phase_breakdown_complete")
                        .IsRequired();

                    consumption.Ignore(value => value.ByPhase);

                    var property = consumption
                        .Property(value => value.ByPhaseStorage)
                        .HasColumnName("tokens_by_phase")
                        .HasColumnType("jsonb")
                        .HasConversion(
                            value => SerializePhases(value),
                            json => DeserializePhases(json))
                        .IsRequired(false);

                    property.Metadata.SetValueComparer(
                        new ValueComparer<Dictionary<ProjectPhase, int>?>(
                            (left, right) => PhaseMapsEqual(left, right),
                            value => PhaseMapHash(value),
                            value => CopyPhaseMap(value)));
                });
        }

        private static string? SerializePhases(
            Dictionary<ProjectPhase, int>? phases) =>
            phases is null
                ? null
                : JsonSerializer.Serialize(phases, TokenJsonOptions);

        private static Dictionary<ProjectPhase, int>? DeserializePhases(
            string? json) =>
            json is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<ProjectPhase, int>>(
                    json,
                    TokenJsonOptions);

        private static bool PhaseMapsEqual(
            Dictionary<ProjectPhase, int>? left,
            Dictionary<ProjectPhase, int>? right) =>
            left is null
                ? right is null
                : right is not null &&
                  left.Count == right.Count &&
                  left.All(entry =>
                      right.TryGetValue(entry.Key, out var value) &&
                      value == entry.Value);

        private static int PhaseMapHash(
            Dictionary<ProjectPhase, int>? phases)
        {
            if (phases is null)
            {
                return 0;
            }

            var hash = new HashCode();

            foreach (var entry in phases.OrderBy(entry => entry.Key))
            {
                hash.Add(entry.Key);
                hash.Add(entry.Value);
            }

            return hash.ToHashCode();
        }

        private static Dictionary<ProjectPhase, int>? CopyPhaseMap(
            Dictionary<ProjectPhase, int>? phases) =>
            phases is null
                ? null
                : new Dictionary<ProjectPhase, int>(phases);

    }
}
