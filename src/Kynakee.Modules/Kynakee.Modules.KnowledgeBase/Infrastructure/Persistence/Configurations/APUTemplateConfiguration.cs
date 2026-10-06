using Kynakee.Modules.KnowledgeBase.Domain.Aggregates;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.KnowledgeBase.Infrastructure.Persistence.Configurations;

public sealed class APUTemplateConfiguration : IEntityTypeConfiguration<APUTemplate>
{
    public void Configure(EntityTypeBuilder<APUTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("apu_templates", table =>
        {
            table.HasCheckConstraint("ck_apu_templates_description", "length(btrim(description)) > 0");
            table.HasCheckConstraint("ck_apu_templates_project_type", "project_type IN ('Residential','Commercial','Industrial','Infrastructure')");
            table.HasCheckConstraint("ck_apu_templates_region", "geo_region ~ '^[A-Z]{2}-[A-Z0-9]{1,7}$'");
            table.HasCheckConstraint("ck_apu_templates_unit", "unit ~ '^[A-Z0-9]+(/[A-Z0-9]+)?$'");
            table.HasCheckConstraint("ck_apu_templates_yield", "yield_hours_per_unit > 0");
            table.HasCheckConstraint("ck_apu_templates_usage", "usage_count >= 0 AND ((usage_count = 0 AND average_confidence IS NULL) OR (usage_count > 0 AND average_confidence BETWEEN 0 AND 1))");
            table.HasCheckConstraint("ck_apu_templates_source", "source IN ('GeneratedByAI','ValidatedByHuman','ImportedFromExternal')");
            table.HasCheckConstraint("ck_apu_templates_deleted", "is_deleted = (deleted_at IS NOT NULL)");
            table.HasCheckConstraint("ck_apu_templates_owner_tenant", "owner_tenant_id IS NULL OR owner_tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_apu_templates_owner_user", "owner_user_id IS NULL OR owner_user_id <> '00000000-0000-0000-0000-000000000000'::uuid");
        });

        builder.HasKey(template => template.Id);

        builder.Property(template => template.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new APUTemplateId(value))
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(template => template.OwnerTenantId).HasColumnName("owner_tenant_id");
        builder.Property(template => template.OwnerUserId).HasColumnName("owner_user_id");

        builder.Property(template => template.CanonicalConceptId)
            .HasColumnName("canonical_concept_id")
            .HasConversion(id => id.Value, value => new CanonicalConceptId(value))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(template => template.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(template => template.ProjectType)
            .HasColumnName("project_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(template => template.Region)
            .HasColumnName("geo_region")
            .HasConversion(region => region.Code, code => new GeoRegion(code))
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(template => template.Unit)
            .HasColumnName("unit")
            .HasConversion(unit => unit.Code, code => new MeasurementUnit(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(template => template.UsageCount)
            .HasColumnName("usage_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(template => template.AverageConfidence)
            .HasColumnName("average_confidence")
            .HasConversion(
                confidence => confidence == null ? (decimal?)null : confidence.Value,
                value => value.HasValue ? new Confidence(value.Value) : null)
            .HasPrecision(4, 3);

        builder.Property(template => template.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(APUTemplateSource.GeneratedByAI)
            .IsRequired();

        builder.Ignore(template => template.EmbeddingVector);
        ConfigureAudit(builder);
        ConfigureProductionYield(builder);
        ConfigureComponents(builder);

        builder.HasOne<CanonicalConcept>()
            .WithMany()
            .HasForeignKey(template => template.CanonicalConceptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(template => new { template.CanonicalConceptId, template.Region })
            .HasDatabaseName("idx_apu_templates_concept")
            .HasFilter("is_deleted = false");
    }

    private static void ConfigureAudit(EntityTypeBuilder<APUTemplate> builder)
    {
        builder.Property(template => template.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
        builder.Property(template => template.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
        builder.Property(template => template.CreatedBy).HasColumnName("created_by");
        builder.Property(template => template.UpdatedBy).HasColumnName("updated_by");
        builder.Property(template => template.DeletedAt).HasColumnName("deleted_at");
        builder.Property(template => template.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(template => template.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();
    }

    private static void ConfigureProductionYield(EntityTypeBuilder<APUTemplate> builder)
    {
        builder.OwnsOne(template => template.ProductionYield, productionYield =>
        {
            productionYield.Property(value => value.HoursPerUnit)
                .HasColumnName("yield_hours_per_unit")
                .HasPrecision(8, 4)
                .IsRequired();

            productionYield.Property(value => value.CrewDescription)
                .HasColumnName("crew_description")
                .HasMaxLength(200);
        });

        builder.Navigation(template => template.ProductionYield).IsRequired();
    }

    private static void ConfigureComponents(EntityTypeBuilder<APUTemplate> builder)
    {
        builder.OwnsMany<APUTemplateComponent>(template => template.Components, components =>
        {
            components.ToTable("apu_template_components", table =>
            {
                table.HasCheckConstraint("ck_apu_components_description", "length(btrim(description)) > 0");
                table.HasCheckConstraint("ck_apu_components_type", "component_type IN ('Material','Labor','Equipment','Subcontract','Transport')");
                table.HasCheckConstraint("ck_apu_components_unit", "unit ~ '^[A-Z0-9]+(/[A-Z0-9]+)?$'");
                table.HasCheckConstraint("ck_apu_components_yield", "yield > 0");
                table.HasCheckConstraint("ck_apu_components_sort", "sort_order >= 0");
            });

            components.Property<Guid>("id")
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");
            components.HasKey("id");

            components.Property<APUTemplateId>("APUTemplateId")
                .HasColumnName("apu_template_id")
                .HasConversion(id => id.Value, value => new APUTemplateId(value))
                .IsRequired();

            components.WithOwner()
                .HasForeignKey("APUTemplateId");

            components.Property(component => component.Description)
                .HasColumnName("description")
                .HasMaxLength(300)
                .IsRequired();

            components.Property(component => component.Type)
                .HasColumnName("component_type")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            components.Property(component => component.Unit)
                .HasColumnName("unit")
                .HasConversion(unit => unit.Code, code => new MeasurementUnit(code))
                .HasMaxLength(20)
                .IsRequired();

            components.Property(component => component.Yield)
                .HasColumnName("yield")
                .HasPrecision(10, 6)
                .IsRequired();

            components.Property(component => component.SortOrder)
                .HasColumnName("sort_order")
                .IsRequired();

            components.HasIndex("APUTemplateId", nameof(APUTemplateComponent.SortOrder))
                .IsUnique()
                .HasDatabaseName("uq_apu_components_position");
        });

        builder.Navigation(template => template.Components)
            .HasField("_components")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}