using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Billing.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("subscriptions", table =>
        {
            table.HasCheckConstraint(
                "ck_billing_subscriptions_period",
                "current_period_end > current_period_start");
            table.HasCheckConstraint(
                "ck_billing_subscriptions_credits",
                "credits_per_cycle > 0");
            table.HasCheckConstraint(
                "ck_billing_subscriptions_deleted",
                "is_deleted = (deleted_at IS NOT NULL)");
        });
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new SubscriptionId(value))
            .ValueGeneratedNever();
        builder.Property(subscription => subscription.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.Property(subscription => subscription.PlanId)
            .HasColumnName("plan_id")
            .HasConversion(planId => planId.Value, value => new PlanId(value))
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(subscription => subscription.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(subscription => subscription.Cycle)
            .HasColumnName("cycle")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(subscription => subscription.CurrentPeriodStart)
            .HasColumnName("current_period_start")
            .IsRequired();
        builder.Property(subscription => subscription.CurrentPeriodEnd)
            .HasColumnName("current_period_end")
            .IsRequired();
        builder.Property(subscription => subscription.StripeSubscriptionId)
            .HasColumnName("stripe_subscription_id")
            .HasMaxLength(100);
        builder.Property(subscription => subscription.CreditsPerCycle)
            .HasColumnName("credits_per_cycle")
            .IsRequired();
        builder.Property(subscription => subscription.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(subscription => subscription.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(subscription => subscription.CreatedBy).HasColumnName("created_by");
        builder.Property(subscription => subscription.UpdatedBy).HasColumnName("updated_by");
        builder.Property(subscription => subscription.DeletedAt).HasColumnName("deleted_at");
        builder.Property(subscription => subscription.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Ignore(nameof(BaseEntity<object>.Version));

        builder.HasOne<CreditAccount>()
            .WithOne()
            .HasForeignKey<Subscription>(subscription => subscription.TenantId)
            .HasPrincipalKey<CreditAccount>(account => account.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(subscription => subscription.StripeSubscriptionId)
            .IsUnique()
            .HasFilter("stripe_subscription_id IS NOT NULL")
            .HasDatabaseName("ux_billing_subscriptions_stripe_id");
    }
}