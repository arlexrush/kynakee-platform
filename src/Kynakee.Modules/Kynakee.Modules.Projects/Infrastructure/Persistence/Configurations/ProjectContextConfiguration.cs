using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.DataContext;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectContextConfiguration :
    IEntityTypeConfiguration<ProjectContext>
{
    public void Configure(EntityTypeBuilder<ProjectContext> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("project_contexts");

        builder.HasKey(context => context.Id);

        builder.HasAlternateKey(context => new
        {
            context.Id,
            context.TenantId
        });

        builder.Property(context => context.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new ProjectContextId(value))
            .ValueGeneratedNever();

        builder.Property(context => context.ProjectId)
            .HasColumnName("project_id")
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(context => context.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(context => context.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(context => context.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(context => context.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(context => context.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(context => context.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(context => context.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(context => context.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithOne(project => project.Context)
            .HasForeignKey<ProjectContext>(context => new
            {
                context.ProjectId,
                context.TenantId
            })
            .HasPrincipalKey<Project>(project => new
            {
                project.Id,
                project.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(
            context => context.Territorial,
            territorial =>
            {
                territorial.Property(value => value.Country)
                    .HasColumnName("territorial_country")
                    .HasMaxLength(3);

                territorial.Property(value => value.Region)
                    .HasColumnName("territorial_region")
                    .HasMaxLength(100);

                territorial.Property(value => value.Province)
                    .HasColumnName("territorial_province")
                    .HasMaxLength(100);

                territorial.Property(value => value.Municipality)
                    .HasColumnName("territorial_municipality")
                    .HasMaxLength(100);

                territorial.Property(value => value.Address)
                    .HasColumnName("territorial_address")
                    .HasMaxLength(300);
            });

        builder.Navigation(context => context.Territorial)
            .IsRequired();

        builder.OwnsOne(
            context => context.Normative,
            normative =>
            {
                normative.Property(value => value.UrbanRegulation)
                    .HasColumnName("normative_urban_regulation");

                normative.Property(value => value.ConstructionCode)
                    .HasColumnName("normative_construction_code")
                    .HasMaxLength(200);
            });

        builder.Navigation(context => context.Normative)
            .IsRequired();

        builder.OwnsOne(
            context => context.Labor,
            labor =>
            {
                labor.Property(value => value.CollectiveAgreement)
                    .HasColumnName("labor_collective_agreement")
                    .HasMaxLength(300);

                labor.Property(value => value.SalaryOfficial1)
                    .HasColumnName("labor_salary_official_1")
                    .HasPrecision(18, 4);

                labor.Property(value => value.SalaryLaborer)
                    .HasColumnName("labor_salary_laborer")
                    .HasPrecision(18, 4);
            });

        builder.Navigation(context => context.Labor)
            .IsRequired();

        builder.OwnsOne(
            context => context.Economic,
            economic =>
            {
                economic.Property(value => value.InflationRate)
                    .HasColumnName("economic_inflation_rate")
                    .HasPrecision(9, 4);

                economic.Property(value => value.VatRate)
                    .HasColumnName("economic_vat_rate")
                    .HasPrecision(9, 4);

                economic.Property(value => value.ConstructionIndex)
                    .HasColumnName("economic_construction_index")
                    .HasPrecision(18, 6);
            });

        builder.Navigation(context => context.Economic)
            .IsRequired();
    }
}
