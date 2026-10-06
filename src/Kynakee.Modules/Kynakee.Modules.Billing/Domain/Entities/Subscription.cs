using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Billing.Domain.Entities;

public sealed class Subscription : BaseEntity<SubscriptionId>
{
    private Subscription()
    {
    }

    private Subscription(
        SubscriptionId id,
        Guid tenantId,
        PlanId planId,
        SubscriptionStatus status,
        BillingCycle cycle,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        string? stripeSubscriptionId,
        int creditsPerCycle,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        PlanId = planId;
        Status = status;
        Cycle = cycle;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        StripeSubscriptionId = stripeSubscriptionId;
        CreditsPerCycle = creditsPerCycle;
    }

    public PlanId PlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public BillingCycle Cycle { get; private set; }

    public DateTime CurrentPeriodStart { get; private set; }

    public DateTime CurrentPeriodEnd { get; private set; }

    public string? StripeSubscriptionId { get; private set; }

    public int CreditsPerCycle { get; private set; }

    /// <summary>Creates an active subscription for a tenant and billing period.</summary>
    public static Result<Subscription> Create(
        Guid tenantId,
        PlanId planId,
        BillingCycle cycle,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        int creditsPerCycle,
        string? stripeSubscriptionId = null,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            return ResultFactory.Failure<Subscription>(
                ApplicationError.Validation(
                    "BILL_TENANT_REQUIRED",
                    "The subscription tenant is required."));
        }

        if (string.IsNullOrWhiteSpace(planId.Value))
        {
            return ResultFactory.Failure<Subscription>(
                ApplicationError.Validation(
                    "BILL_PLAN_REQUIRED",
                    "The subscription plan is required."));
        }

        if (!Enum.IsDefined(cycle))
        {
            return ResultFactory.Failure<Subscription>(
                ApplicationError.Validation(
                    "BILL_CYCLE_INVALID",
                    "The billing cycle is invalid."));
        }

        if (currentPeriodStart.Kind != DateTimeKind.Utc ||
            currentPeriodEnd.Kind != DateTimeKind.Utc ||
            currentPeriodEnd <= currentPeriodStart)
        {
            return ResultFactory.Failure<Subscription>(
                ApplicationError.Validation(
                    "BILL_PERIOD_INVALID",
                    "The subscription period must be a valid UTC interval."));
        }

        if (creditsPerCycle <= 0)
        {
            return ResultFactory.Failure<Subscription>(
                ApplicationError.Validation(
                    "BILL_CYCLE_CREDITS_INVALID",
                    "Credits per billing cycle must be greater than zero."));
        }

        if (stripeSubscriptionId is not null &&
            string.IsNullOrWhiteSpace(stripeSubscriptionId))
        {
            return ResultFactory.Failure<Subscription>(
                ApplicationError.Validation(
                    "BILL_STRIPE_SUBSCRIPTION_INVALID",
                    "The Stripe subscription identifier cannot be blank."));
        }

        return ResultFactory.Success(
            new Subscription(
                SubscriptionId.New(),
                tenantId,
                planId,
                SubscriptionStatus.Active,
                cycle,
                currentPeriodStart,
                currentPeriodEnd,
                stripeSubscriptionId,
                creditsPerCycle,
                createdBy));
    }

    /// <summary>Changes subscription status while enforcing terminal cancellation rules.</summary>
    public Result ChangeStatus(SubscriptionStatus status, Guid? updatedBy = null)
    {
        if (!Enum.IsDefined(status))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation(
                    "BILL_SUBSCRIPTION_STATUS_INVALID",
                    "The subscription status is invalid."));
        }

        if (Status == SubscriptionStatus.Cancelled &&
            status == SubscriptionStatus.PastDue)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "BILL_SUBSCRIPTION_STATUS_CONFLICT",
                    "A cancelled subscription cannot become past due."));
        }

        if (Status != status)
        {
            Status = status;
            RegisterUpdate(updatedBy);
        }

        return ResultFactory.Ok();
    }

    /// <summary>Advances the billing period for an active subscription.</summary>
    public Result Renew(
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        Guid? updatedBy = null)
    {
        if (Status != SubscriptionStatus.Active)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "BILL_SUBSCRIPTION_NOT_ACTIVE",
                    "Only an active subscription can be renewed."));
        }

        if (currentPeriodStart.Kind != DateTimeKind.Utc ||
            currentPeriodEnd.Kind != DateTimeKind.Utc ||
            currentPeriodStart < CurrentPeriodEnd ||
            currentPeriodEnd <= currentPeriodStart)
        {
            return ResultFactory.Failure(
                ApplicationError.Validation(
                    "BILL_PERIOD_INVALID",
                    "The renewed subscription period must follow the current period."));
        }

        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        RegisterUpdate(updatedBy);

        return ResultFactory.Ok();
    }
}