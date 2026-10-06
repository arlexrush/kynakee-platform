using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Domain.ValueObjects;

public readonly record struct CreditAccountId(Guid Value)
{
    public static CreditAccountId New() => new(Guid.NewGuid());
}

public readonly record struct CreditLotId(Guid Value)
{
    public static CreditLotId New() => new(Guid.NewGuid());
}

public readonly record struct SubscriptionId(Guid Value)
{
    public static SubscriptionId New() => new(Guid.NewGuid());
}

public readonly record struct PlanId(string Value)
{
    /// <summary>Creates a plan identifier after rejecting blank values.</summary>
    public static Result<PlanId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ResultFactory.Failure<PlanId>(
                ApplicationError.Validation(
                    "BILL_PLAN_REQUIRED",
                    "The billing plan identifier is required."));
        }

        return ResultFactory.Success(new PlanId(value.Trim()));
    }
}