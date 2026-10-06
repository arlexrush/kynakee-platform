using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Billing.Domain.Events;

public sealed record CreditsReservedEvent(
    CreditAccountId CreditAccountId,
    Guid TenantId,
    Guid OperationId,
    decimal Amount) : DomainEvent;

public sealed record CreditsConsumedEvent(
    CreditAccountId CreditAccountId,
    Guid TenantId,
    Guid OperationId,
    decimal Amount) : DomainEvent;

public sealed record CreditsReleasedEvent(
    CreditAccountId CreditAccountId,
    Guid TenantId,
    Guid OperationId,
    decimal Amount) : DomainEvent;

public sealed record CreditsRechargedEvent(
    CreditAccountId CreditAccountId,
    Guid TenantId,
    CreditLotId CreditLotId,
    decimal Amount,
    CreditSource Source,
    DateTime ExpiresAt) : DomainEvent;

public sealed record CreditLowWarningEvent(
    CreditAccountId CreditAccountId,
    Guid TenantId,
    decimal AvailableCredits) : DomainEvent;

public sealed record CreditDepletedEvent(
    CreditAccountId CreditAccountId,
    Guid TenantId) : DomainEvent;

public sealed record SubscriptionRenewedEvent(
    Guid TenantId,
    PlanId PlanId,
    decimal CreditsAdded,
    DateTime NextRenewalDate) : DomainEvent;