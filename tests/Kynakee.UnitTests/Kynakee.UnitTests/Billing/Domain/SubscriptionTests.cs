using FluentAssertions;
using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Billing.Domain;

public class SubscriptionTests
{
    private static readonly DateTime PeriodStart =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateShouldInitializeActiveSubscription()
    {
        var subscription = CreateSubscription();

        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.CreditsPerCycle.Should().Be(500);
    }

    [Fact]
    public void CreateShouldRejectInvalidPeriod()
    {
        var result = Subscription.Create(
            Guid.NewGuid(),
            CreatePlanId(),
            BillingCycle.Monthly,
            PeriodStart,
            PeriodStart,
            500);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_PERIOD_INVALID");
    }

    [Fact]
    public void RenewShouldAdvancePeriodForActiveSubscription()
    {
        var subscription = CreateSubscription();
        var nextPeriodEnd = PeriodStart.AddMonths(2);

        var result = subscription.Renew(
            PeriodStart.AddMonths(1),
            nextPeriodEnd);

        result.IsSuccess.Should().BeTrue();
        subscription.CurrentPeriodStart.Should().Be(PeriodStart.AddMonths(1));
        subscription.CurrentPeriodEnd.Should().Be(nextPeriodEnd);
    }

    [Fact]
    public void RenewShouldRejectCancelledSubscription()
    {
        var subscription = CreateSubscription();
        subscription.ChangeStatus(SubscriptionStatus.Cancelled);

        var result = subscription.Renew(
            PeriodStart.AddMonths(1),
            PeriodStart.AddMonths(2));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_SUBSCRIPTION_NOT_ACTIVE");
    }

    [Fact]
    public void CancelledSubscriptionShouldNotBecomePastDue()
    {
        var subscription = CreateSubscription();
        subscription.ChangeStatus(SubscriptionStatus.Cancelled);

        var result = subscription.ChangeStatus(SubscriptionStatus.PastDue);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_SUBSCRIPTION_STATUS_CONFLICT");
    }

    private static Subscription CreateSubscription() => Subscription.Create(
        Guid.NewGuid(),
        CreatePlanId(),
        BillingCycle.Monthly,
        PeriodStart,
        PeriodStart.AddMonths(1),
        500).Value!;

    private static PlanId CreatePlanId() => PlanId.Create("starter").Value;
}