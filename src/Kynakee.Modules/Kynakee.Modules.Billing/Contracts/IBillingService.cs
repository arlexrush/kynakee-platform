using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Contracts;

/// <summary>Public tenant-scoped contract for Billing operations.</summary>
public interface IBillingService
{
    /// <summary>Reserves credits for an operation and returns its expiry.</summary>
    Task<Result<CreditReservation>> ReserveCreditsAsync(
        Guid tenantId,
        decimal amount,
        Guid operationId,
        CancellationToken cancellationToken);

    /// <summary>Consumes a previously reserved credit amount.</summary>
    Task<Result> ConsumeCreditsAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken);

    /// <summary>Releases a previously reserved credit amount.</summary>
    Task<Result> ReleaseCreditsAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken);

    /// <summary>Returns the current credit balance and renewal date.</summary>
    Task<Result<CreditBalanceDto>> GetBalanceAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Indicates whether the tenant has the requested available credits.</summary>
    Task<Result<bool>> HasSufficientCreditsAsync(
        Guid tenantId,
        decimal requiredAmount,
        CancellationToken cancellationToken);

    /// <summary>Returns a cursor-paginated credit transaction history.</summary>
    Task<Result<CreditTransactionHistoryDto>> GetTransactionsAsync(
        Guid tenantId,
        CreditTransactionKind? type,
        DateTime? from,
        DateTime? endDate,
        string? cursor,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Creates the initial account and subscription for a new tenant.</summary>
    Task<Result> InitializeCreditAccountAsync(
        Guid tenantId,
        string planId,
        CancellationToken cancellationToken);
}

public sealed record CreditReservation(
    Guid OperationId,
    decimal Amount,
    DateTime ExpiresAt);

public sealed record CreditBalanceDto(
    decimal AvailableCredits,
    decimal ReservedCredits,
    string PlanId,
    DateTime? NextRenewalDate);

public enum CreditTransactionKind
{
    Reserve,
    Consume,
    Release,
    Recharge
}

public sealed record CreditTransactionHistoryItem(
    Guid Id,
    Guid? OperationId,
    CreditTransactionKind Type,
    decimal Amount,
    string? Source,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

public sealed record CreditTransactionHistoryDto(
    IReadOnlyList<CreditTransactionHistoryItem> Items,
    string? NextCursor);