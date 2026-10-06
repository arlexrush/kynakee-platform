using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.DataCapture;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class CaptureExpedientConfiguration :
    IEntityTypeConfiguration<CaptureExpedient>
{
    public void Configure(EntityTypeBuilder<CaptureExpedient> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("capture_expedients");

        builder.HasKey(capture => capture.Id);

        builder.HasAlternateKey(capture => new
        {
            capture.Id,
            capture.TenantId
        });

        builder.Property(capture => capture.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new CaptureExpedientId(value))
            .ValueGeneratedNever();

        builder.Property(capture => capture.ProjectId)
            .HasColumnName("project_id")
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(capture => capture.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(capture => capture.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(capture => capture.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(capture => capture.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(capture => capture.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(capture => capture.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(capture => capture.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(capture => capture.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithOne(project => project.Capture)
            .HasForeignKey<CaptureExpedient>(capture => new
            {
                capture.ProjectId,
                capture.TenantId
            })
            .HasPrincipalKey<Project>(project => new
            {
                project.Id,
                project.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(
            capture => capture.MediaFiles,
            media =>
            {
                ConfigureOwnedCollection(media, "capture_media_files");

                media.Property(item => item.Url)
                    .HasColumnName("url")
                    .HasConversion(
                        url => url.OriginalString,
                        value => new Uri(value, UriKind.RelativeOrAbsolute))
                    .IsRequired();

                media.Property(item => item.Type)
                    .HasColumnName("type")
                    .HasMaxLength(100)
                    .IsRequired();

                media.Property(item => item.Room)
                    .HasColumnName("room")
                    .HasMaxLength(200);

                media.Property(item => item.IsPathology)
                    .HasColumnName("is_pathology")
                    .IsRequired();

                media.Property(item => item.Processed)
                    .HasColumnName("processed")
                    .IsRequired();
            });

        builder.Navigation(capture => capture.MediaFiles)
            .HasField("_mediaFiles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(
            capture => capture.Measurements,
            measurement =>
            {
                ConfigureOwnedCollection(
                    measurement,
                    "capture_measurements");

                measurement.Property(item => item.Description)
                    .HasColumnName("description")
                    .HasMaxLength(500)
                    .IsRequired();

                measurement.Property(item => item.Value)
                    .HasColumnName("value")
                    .HasPrecision(18, 6)
                    .IsRequired();

                measurement.Property(item => item.Unit)
                    .HasColumnName("unit")
                    .HasConversion(
                        unit => unit.Code,
                        code => ResolveUnit(code))
                    .HasMaxLength(20)
                    .IsRequired();
            });

        builder.Navigation(capture => capture.Measurements)
            .HasField("_measurements")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(
            capture => capture.Transcriptions,
            transcription =>
            {
                ConfigureOwnedCollection(
                    transcription,
                    "capture_transcriptions");

                transcription.Property(item => item.Text)
                    .HasColumnName("text")
                    .IsRequired();

                transcription.Property(item => item.Source)
                    .HasColumnName("source")
                    .HasMaxLength(200);
            });

        builder.Navigation(capture => capture.Transcriptions)
            .HasField("_transcriptions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(
            capture => capture.Observations,
            observation =>
            {
                ConfigureOwnedCollection(
                    observation,
                    "capture_observations");

                observation.Property(item => item.Description)
                    .HasColumnName("description")
                    .HasMaxLength(500)
                    .IsRequired();

                observation.Property(item => item.Room)
                    .HasColumnName("room")
                    .HasMaxLength(200);

                observation.Property(item => item.Severity)
                    .HasColumnName("severity")
                    .HasMaxLength(50);
            });

        builder.Navigation(capture => capture.Observations)
            .HasField("_observations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureOwnedCollection<TItem>(
        OwnedNavigationBuilder<CaptureExpedient, TItem> builder,
        string tableName)
        where TItem : class
    {
        builder.ToTable(tableName);

        builder.Property<CaptureExpedientId>("CaptureExpedientId")
            .HasColumnName("capture_expedient_id")
            .HasConversion(
                id => id.Value,
                value => new CaptureExpedientId(value));

        builder.Property<Guid>("TenantId")
            .HasColumnName("tenant_id");

        builder.Property<Guid>("EntryId")
            .HasColumnName("entry_id")
            .ValueGeneratedOnAdd();

        builder.HasKey(
            "CaptureExpedientId",
            "TenantId",
            "EntryId");

        builder.WithOwner()
            .HasForeignKey(
                "CaptureExpedientId",
                "TenantId")
            .HasPrincipalKey(
                nameof(CaptureExpedient.Id),
                nameof(CaptureExpedient.TenantId));
    }

    private static MeasurementUnit ResolveUnit(string code) =>
        code switch
        {
            "M2" => MeasurementUnit.SquareMeter,
            "M3" => MeasurementUnit.CubicMeter,
            "ML" => MeasurementUnit.LinearMeter,
            "UN" => MeasurementUnit.Unit,
            "KG" => MeasurementUnit.Kilogram,
            "TN" => MeasurementUnit.Ton,
            "HR" => MeasurementUnit.Hour,
            "DY" => MeasurementUnit.Day,
            _ => ResolveCompositeUnit(code)
        };

    private static MeasurementUnit ResolveCompositeUnit(string code)
    {
        var parts = code.Split('/', StringSplitOptions.TrimEntries);

        if (parts.Length != 2 ||
            parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"Unknown capture measurement unit '{code}'.");
        }

        return MeasurementUnit.Composite(parts[0], parts[1]);
    }
}
