using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;

namespace Kynakee.Modules.Billing.Application.Abstractions;

public interface IBillingRepository
{
    Task<CreditAccount?> GetAccountForUpdateAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<Subscription?> GetSubscriptionForUpdateAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<CreditBalanceProjection?> GetBalanceAsync(
        Guid tenantId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<CreditTransactionPageProjection> GetTransactionsAsync(
        Guid tenantId,
        CreditTransactionType? type,
        DateTime? from,
        DateTime? endDate,
        string? cursor,
        int limit,
        CancellationToken cancellationToken);

    void AddAccount(CreditAccount account);

    void AddSubscription(Subscription subscription);
}

public sealed record CreditBalanceProjection(
    decimal AvailableCredits,
    decimal ReservedCredits,
    string PlanId,
    DateTime? NextRenewalDate);

public sealed record CreditTransactionProjection(
    Guid Id,
    Guid? OperationId,
    CreditTransactionType Type,
    decimal Amount,
    CreditSource? Source,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

public sealed record CreditTransactionPageProjection(
    IReadOnlyList<CreditTransactionProjection> Items,
    string? NextCursor);