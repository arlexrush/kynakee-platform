using Kynakee.Modules.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tenants", table =>
        {
            table.HasCheckConstraint("ck_identity_tenants_tenant_id", "tenant_id = id");
            table.HasCheckConstraint("ck_identity_tenants_deleted", "is_deleted = (deleted_at IS NOT NULL)");
        });
        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(tenant => tenant.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.Property(tenant => tenant.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(tenant => tenant.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(tenant => tenant.PlanId)
            .HasColumnName("plan_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(tenant => tenant.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.OwnsOne(tenant => tenant.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(63)
                .IsRequired();
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasFilter("\"is_deleted\" = false")
                .HasDatabaseName("ux_identity_tenants_slug");
        });
        builder.Navigation(tenant => tenant.Slug).IsRequired();

        builder.OwnsOne(tenant => tenant.TaxId, taxId =>
        {
            taxId.Property(value => value.Value)
                .HasColumnName("tax_id")
                .HasMaxLength(32);
            taxId.Property(value => value.CountryCode)
                .HasColumnName("tax_country")
                .HasMaxLength(2);
        });
        builder.Navigation(tenant => tenant.TaxId).IsRequired(false);

        builder.OwnsOne(tenant => tenant.FiscalAddress, address =>
        {
            address.Property(value => value.Country)
                .HasColumnName("fiscal_country")
                .HasMaxLength(2)
                .IsRequired();
            address.Property(value => value.Region)
                .HasColumnName("fiscal_region")
                .HasMaxLength(100);
            address.Property(value => value.Province)
                .HasColumnName("fiscal_province")
                .HasMaxLength(100);
            address.Property(value => value.Municipality)
                .HasColumnName("fiscal_municipality")
                .HasMaxLength(100);
            address.Property(value => value.PostalCode)
                .HasColumnName("fiscal_postal_code")
                .HasMaxLength(20);
            address.Property(value => value.Street)
                .HasColumnName("fiscal_street")
                .HasMaxLength(300);
        });
        builder.Navigation(tenant => tenant.FiscalAddress).IsRequired(false);

        builder.OwnsOne(tenant => tenant.Branding, branding =>
        {
            branding.Property(value => value.CompanyName)
                .HasColumnName("branding_company_name")
                .HasMaxLength(200);
            branding.Property(value => value.PrimaryColor)
                .HasColumnName("branding_primary_color")
                .HasMaxLength(7);
            branding.Property(value => value.LogoUrl)
                .HasColumnName("branding_logo_url")
                .HasConversion(
                    logoUrl => logoUrl == null ? null : logoUrl.AbsoluteUri,
                    value => value == null ? null : new Uri(value, UriKind.Absolute))
                .HasMaxLength(2048);
            branding.Property(value => value.Email)
                .HasColumnName("branding_email")
                .HasMaxLength(320);
        });
        builder.Navigation(tenant => tenant.Branding).IsRequired();

        builder.OwnsOne(tenant => tenant.Settings, settings =>
        {
            settings.Property(value => value.Administration)
                .HasColumnName("settings_administration")
                .HasPrecision(5, 2)
                .IsRequired();
            settings.Property(value => value.Profit)
                .HasColumnName("settings_profit")
                .HasPrecision(5, 2)
                .IsRequired();
            settings.Property(value => value.Quality)
                .HasColumnName("settings_quality")
                .HasPrecision(5, 2)
                .IsRequired();
            settings.Property(value => value.SafetyHealth)
                .HasColumnName("settings_safety_health")
                .HasPrecision(5, 2)
                .IsRequired();
            settings.Property(value => value.Environment)
                .HasColumnName("settings_environment")
                .HasPrecision(5, 2)
                .IsRequired();
            settings.Property(value => value.Contingency)
                .HasColumnName("settings_contingency")
                .HasPrecision(5, 2)
                .IsRequired();
        });
        builder.Navigation(tenant => tenant.Settings).IsRequired();

        builder.Property(tenant => tenant.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(tenant => tenant.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(tenant => tenant.CreatedBy).HasColumnName("created_by");
        builder.Property(tenant => tenant.UpdatedBy).HasColumnName("updated_by");
        builder.Property(tenant => tenant.DeletedAt).HasColumnName("deleted_at");
        builder.Property(tenant => tenant.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(tenant => tenant.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasAlternateKey(tenant => new { tenant.TenantId, tenant.Id })
            .HasName("ak_identity_tenants_tenant_id");
    }
}