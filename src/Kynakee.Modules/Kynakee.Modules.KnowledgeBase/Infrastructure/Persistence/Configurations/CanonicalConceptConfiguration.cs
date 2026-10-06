using Kynakee.Modules.KnowledgeBase.Domain.Aggregates;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.KnowledgeBase.Infrastructure.Persistence.Configurations;

public sealed class CanonicalConceptConfiguration : IEntityTypeConfiguration<CanonicalConcept>
{
    public void Configure(EntityTypeBuilder<CanonicalConcept> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("canonical_concepts", table =>
        {
            table.HasCheckConstraint("ck_canonical_concepts_id", "id ~ '^[A-Z0-9_]{1,100}$'");
            table.HasCheckConstraint("ck_canonical_concepts_category", "length(btrim(category)) > 0");
            table.HasCheckConstraint("ck_canonical_concepts_unit", "default_unit ~ '^[A-Z0-9]+(/[A-Z0-9]+)?$'");
            table.HasCheckConstraint("ck_canonical_concepts_count", "apu_template_count >= 0");
            table.HasCheckConstraint("ck_canonical_concepts_deleted", "is_deleted = (deleted_at IS NOT NULL)");
            table.HasCheckConstraint("ck_canonical_concepts_owner_tenant", "owner_tenant_id IS NULL OR owner_tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_canonical_concepts_owner_user", "owner_user_id IS NULL OR owner_user_id <> '00000000-0000-0000-0000-000000000000'::uuid");
        });

        builder.HasKey(concept => concept.Id);

        builder.Property(concept => concept.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new CanonicalConceptId(value))
            .HasMaxLength(100)
            .ValueGeneratedNever();

        builder.Property(concept => concept.OwnerTenantId).HasColumnName("owner_tenant_id");
        builder.Property(concept => concept.OwnerUserId).HasColumnName("owner_user_id");

        builder.Property(concept => concept.Category)
            .HasColumnName("category")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(concept => concept.Subcategory)
            .HasColumnName("subcategory")
            .HasMaxLength(100);

        builder.Property(concept => concept.DefaultUnit)
            .HasColumnName("default_unit")
            .HasConversion(unit => unit.Code, code => new MeasurementUnit(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(concept => concept.APUTemplateCount)
            .HasColumnName("apu_template_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Ignore(concept => concept.EmbeddingVector);

        ConfigureAudit(builder);
        ConfigureTranslations(builder);
    }

    private static void ConfigureAudit(EntityTypeBuilder<CanonicalConcept> builder)
    {
        builder.Property(concept => concept.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
        builder.Property(concept => concept.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
        builder.Property(concept => concept.CreatedBy).HasColumnName("created_by");
        builder.Property(concept => concept.UpdatedBy).HasColumnName("updated_by");
        builder.Property(concept => concept.DeletedAt).HasColumnName("deleted_at");
        builder.Property(concept => concept.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(concept => concept.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();
    }

    private static void ConfigureTranslations(EntityTypeBuilder<CanonicalConcept> builder)
    {
        builder.OwnsMany<ConceptTranslation>(concept => concept.Translations, translations =>
        {
            translations.ToTable("concept_translations", table =>
            {
                table.HasCheckConstraint("ck_concept_translations_language", "language_code ~ '^[A-Z]{2}(-[A-Z]{2})?$'");
                table.HasCheckConstraint("ck_concept_translations_name", "length(btrim(name)) > 0");
            });

            translations.Property<Guid>("id")
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");
            translations.HasKey("id");

            translations.Property<CanonicalConceptId>("CanonicalConceptId")
                .HasColumnName("concept_id")
                .HasConversion(id => id.Value, value => new CanonicalConceptId(value))
                .HasMaxLength(100)
                .IsRequired();

            translations.WithOwner()
                .HasForeignKey("CanonicalConceptId");

            translations.Property(translation => translation.LanguageCode)
                .HasColumnName("language_code")
                .HasMaxLength(5)
                .IsRequired();

            translations.Property(translation => translation.Name)
                .HasColumnName("name")
                .HasMaxLength(300)
                .IsRequired();

            translations.Property(translation => translation.Description)
                .HasColumnName("description");

            translations.Property<DateTime>("CreatedAt")
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()")
                .IsRequired();

            translations.HasIndex("CanonicalConceptId", nameof(ConceptTranslation.LanguageCode))
                .IsUnique()
                .HasDatabaseName("idx_concept_translations_unique");
        });

        builder.Navigation(concept => concept.Translations)
            .HasField("_translations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}