namespace Kynakee.Modules.Billing.Domain.ValueObjects;

public enum CreditSource
{
    Stripe,
    Manual,
    Promotional,
    Subscription
}

public enum CreditTransactionType
{
    Reservation,
    Consumed,
    Released,
    Recharged
}

public enum SubscriptionStatus
{
    Active,
    Cancelled,
    PastDue
}

public enum BillingCycle
{
    Monthly,
    Annual
}

public sealed record CreditReservationAllocation(
    CreditLotId CreditLotId,
    decimal Amount);

public sealed record CreditReservation(
    Guid OperationId,
    decimal Amount,
    DateTime ExpiresAt,
    IReadOnlyList<CreditReservationAllocation> Allocations);