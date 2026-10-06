using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.Events;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Billing.Domain.Aggregates;

public sealed class CreditAccount : AggregateRoot<CreditAccountId>
{
    private const decimal LowCreditThreshold = 50m;

    private readonly List<CreditLot> _creditLots = [];
    private readonly List<CreditTransaction> _transactions = [];

    private CreditAccount()
    {
    }

    private CreditAccount(
        CreditAccountId id,
        Guid tenantId,
        PlanId planId,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        PlanId = planId;
    }

    public PlanId PlanId { get; private set; }

    public decimal AvailableCredits => CalculateAvailableCredits(DateTime.UtcNow);

    public decimal ReservedCredits => _creditLots.Sum(lot => lot.ReservedCredits);

    public IReadOnlyList<CreditLot> CreditLots => _creditLots.AsReadOnly();

    public IReadOnlyList<CreditTransaction> Transactions => _transactions.AsReadOnly();

    /// <summary>Creates an empty credit account for a tenant and plan.</summary>
    public static Result<CreditAccount> Create(
        Guid tenantId,
        PlanId planId,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            return ResultFactory.Failure<CreditAccount>(
                ApplicationError.Validation(
                    "BILL_TENANT_REQUIRED",
                    "The credit account tenant is required."));
        }

        if (string.IsNullOrWhiteSpace(planId.Value))
        {
            return ResultFactory.Failure<CreditAccount>(
                ApplicationError.Validation(
                    "BILL_PLAN_REQUIRED",
                    "The credit account plan is required."));
        }

        return ResultFactory.Success(
            new CreditAccount(CreditAccountId.New(), tenantId, planId, createdBy));
    }

    /// <summary>Calculates unreserved credits that have not expired at the specified UTC time.</summary>
    public decimal CalculateAvailableCredits(DateTime utcNow) =>
        _creditLots
            .Where(lot => lot.ExpiresAt > utcNow)
            .Sum(lot => lot.AvailableCredits);

    /// <summary>Reserves available credits from the earliest-expiring eligible lots.</summary>
    public Result<CreditReservation> Reserve(
        decimal amount,
        Guid operationId,
        DateTime expiresAt,
        DateTime utcNow,
        Guid? updatedBy = null)
    {
        if (amount <= 0)
        {
            return ResultFactory.Failure<CreditReservation>(
                ApplicationError.Validation(
                    "BILL_AMOUNT_INVALID",
                    "The credit reservation amount must be greater than zero."));
        }

        if (operationId == Guid.Empty)
        {
            return ResultFactory.Failure<CreditReservation>(
                ApplicationError.Validation(
                    "BILL_OPERATION_REQUIRED",
                    "The credit reservation operation identifier is required."));
        }

        if (utcNow.Kind != DateTimeKind.Utc ||
            expiresAt.Kind != DateTimeKind.Utc ||
            expiresAt <= utcNow)
        {
            return ResultFactory.Failure<CreditReservation>(
                ApplicationError.Validation(
                    "BILL_RESERVATION_EXPIRY_INVALID",
                    "The reservation expiry must be after the current UTC time."));
        }

        var latestOperation = GetLatestOperation(operationId);
        if (latestOperation is not null)
        {
            if (latestOperation.Type == CreditTransactionType.Reservation &&
                latestOperation.Amount == amount)
            {
                return ResultFactory.Success(
                    new CreditReservation(
                        operationId,
                        latestOperation.Amount,
                        latestOperation.ExpiresAt!.Value,
                        latestOperation.Allocations));
            }

            return ResultFactory.Failure<CreditReservation>(
                ApplicationError.Conflict(
                    "BILL_OPERATION_FINALIZED",
                    "The operation identifier was already used for a different or finalized reservation."));
        }

        if (CalculateAvailableCredits(utcNow) < amount)
        {
            return ResultFactory.Failure<CreditReservation>(
                ApplicationError.Credits(
                    "BILL_INSUFFICIENT_CREDITS",
                    "The available credit balance is insufficient."));
        }

        var previousBalance = CalculateAvailableCredits(utcNow);
        var allocations = AllocateCredits(amount, expiresAt, utcNow);
        if (allocations is null)
        {
            return ResultFactory.Failure<CreditReservation>(
                ApplicationError.Credits(
                    "BILL_INSUFFICIENT_CREDITS",
                    "The available credit balance is insufficient."));
        }

        foreach (var allocation in allocations.Value.Allocations)
        {
            allocation.Lot.Reserve(allocation.Amount, updatedBy);
        }

        var transactionAllocations = allocations.Value.Allocations
            .Select(allocation => new CreditReservationAllocation(
                allocation.Lot.Id,
                allocation.Amount))
            .ToArray();
        var reservation = new CreditReservation(
            operationId,
            amount,
            allocations.Value.ExpiresAt,
            transactionAllocations);

        _transactions.Add(CreditTransaction.Create(
            TenantId,
            CreditTransactionType.Reservation,
            amount,
            operationId,
            null,
            reservation.ExpiresAt,
            transactionAllocations,
            updatedBy));
        RegisterUpdate(updatedBy);
        AddLowCreditWarningIfCrossed(previousBalance, CalculateAvailableCredits(utcNow));
        AddDomainEvent(new CreditsReservedEvent(Id, TenantId, operationId, amount));

        return ResultFactory.Success(reservation);
    }

    /// <summary>Consumes an active reservation once and records the lot allocations.</summary>
    public Result Consume(
        Guid operationId,
        DateTime utcNow,
        Guid? updatedBy = null)
    {
        var latestOperation = GetLatestOperation(operationId);
        if (latestOperation is null)
        {
            return ResultFactory.Failure(
                ApplicationError.NotFound(
                    "BILL_RESERVATION_NOT_FOUND",
                    "The credit reservation was not found."));
        }

        if (latestOperation.Type == CreditTransactionType.Consumed)
        {
            return ResultFactory.Ok();
        }

        if (latestOperation.Type != CreditTransactionType.Reservation)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "BILL_RESERVATION_FINALIZED",
                    "The credit reservation is no longer active."));
        }

        if (utcNow.Kind != DateTimeKind.Utc || latestOperation.ExpiresAt <= utcNow)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "BILL_RESERVATION_EXPIRED",
                    "The credit reservation has expired."));
        }

        var allocatedLots = latestOperation.Allocations
            .Select(allocation => (
                Allocation: allocation,
                Lot: _creditLots.SingleOrDefault(item => item.Id == allocation.CreditLotId)))
            .ToArray();
        if (allocatedLots.Any(item => item.Lot is null))
        {
            return ResultFactory.Failure(
                ApplicationError.Unexpected(
                    "BILL_RESERVATION_LOT_MISSING",
                    "A reserved credit lot could not be found."));
        }

        foreach (var item in allocatedLots)
        {
            item.Lot!.ConsumeReservation(item.Allocation.Amount, updatedBy);
        }

        _transactions.Add(CreditTransaction.Create(
            TenantId,
            CreditTransactionType.Consumed,
            latestOperation.Amount,
            operationId,
            null,
            null,
            latestOperation.Allocations,
            updatedBy));
        RegisterUpdate(updatedBy);
        AddDomainEvent(new CreditsConsumedEvent(
            Id,
            TenantId,
            operationId,
            latestOperation.Amount));
        RaiseDepletedEventIfNeeded(utcNow);

        return ResultFactory.Ok();
    }

    /// <summary>Releases an active reservation back to its original credit lots.</summary>
    public Result Release(Guid operationId, Guid? updatedBy = null)
    {
        var latestOperation = GetLatestOperation(operationId);
        if (latestOperation is null)
        {
            return ResultFactory.Failure(
                ApplicationError.NotFound(
                    "BILL_RESERVATION_NOT_FOUND",
                    "The credit reservation was not found."));
        }

        if (latestOperation.Type == CreditTransactionType.Released)
        {
            return ResultFactory.Ok();
        }

        if (latestOperation.Type != CreditTransactionType.Reservation)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "BILL_RESERVATION_FINALIZED",
                    "The credit reservation is no longer active."));
        }

        var allocatedLots = latestOperation.Allocations
            .Select(allocation => (
                Allocation: allocation,
                Lot: _creditLots.SingleOrDefault(item => item.Id == allocation.CreditLotId)))
            .ToArray();
        if (allocatedLots.Any(item => item.Lot is null))
        {
            return ResultFactory.Failure(
                ApplicationError.Unexpected(
                    "BILL_RESERVATION_LOT_MISSING",
                    "A reserved credit lot could not be found."));
        }

        foreach (var item in allocatedLots)
        {
            item.Lot!.ReleaseReservation(item.Allocation.Amount, updatedBy);
        }

        _transactions.Add(CreditTransaction.Create(
            TenantId,
            CreditTransactionType.Released,
            latestOperation.Amount,
            operationId,
            null,
            null,
            latestOperation.Allocations,
            updatedBy));
        RegisterUpdate(updatedBy);
        AddDomainEvent(new CreditsReleasedEvent(
            Id,
            TenantId,
            operationId,
            latestOperation.Amount));

        return ResultFactory.Ok();
    }

    /// <summary>Adds a credit lot and records its origin and expiry date.</summary>
    public Result<CreditLotId> Recharge(
        decimal amount,
        CreditSource source,
        DateTime expiresAt,
        DateTime purchasedAt,
        Guid? createdBy = null)
    {
        var previousBalance = CalculateAvailableCredits(purchasedAt);
        var lotResult = CreditLot.Create(
            TenantId,
            amount,
            source,
            purchasedAt,
            expiresAt,
            createdBy);
        if (lotResult.IsFailure)
        {
            return ResultFactory.Failure<CreditLotId>(lotResult.Error!);
        }

        var lot = lotResult.Value!;
        _creditLots.Add(lot);
        _transactions.Add(CreditTransaction.Create(
            TenantId,
            CreditTransactionType.Recharged,
            amount,
            null,
            source,
            expiresAt,
            [new CreditReservationAllocation(lot.Id, amount)],
            createdBy));
        RegisterUpdate(createdBy);
        AddLowCreditWarningIfCrossed(previousBalance, CalculateAvailableCredits(purchasedAt));
        AddDomainEvent(new CreditsRechargedEvent(
            Id,
            TenantId,
            lot.Id,
            amount,
            source,
            expiresAt));

        return ResultFactory.Success(lot.Id);
    }

    private CreditTransaction? GetLatestOperation(Guid operationId) =>
        operationId == Guid.Empty
            ? null
            : _transactions.LastOrDefault(transaction =>
                transaction.OperationId == operationId);

    private (IReadOnlyList<(CreditLot Lot, decimal Amount)> Allocations, DateTime ExpiresAt)?
        AllocateCredits(decimal amount, DateTime requestedExpiry, DateTime utcNow)
    {
        var remaining = amount;
        var allocations = new List<(CreditLot Lot, decimal Amount)>();

        foreach (var lot in _creditLots
                     .Where(item => item.IsAvailableAt(utcNow))
                     .OrderBy(item => item.ExpiresAt)
                     .ThenBy(item => item.PurchasedAt)
                     .ThenBy(item => item.Id.Value))
        {
            var allocated = Math.Min(remaining, lot.AvailableCredits);
            if (allocated <= 0)
            {
                continue;
            }

            allocations.Add((lot, allocated));
            remaining -= allocated;
            if (remaining == 0)
            {
                break;
            }
        }

        if (remaining > 0)
        {
            return null;
        }

        var expiresAt = allocations.Min(allocation => allocation.Lot.ExpiresAt);
        if (requestedExpiry < expiresAt)
        {
            expiresAt = requestedExpiry;
        }

        return (allocations, expiresAt);
    }

    private void AddLowCreditWarningIfCrossed(
        decimal previousBalance,
        decimal currentBalance)
    {
        if (previousBalance >= LowCreditThreshold &&
            currentBalance < LowCreditThreshold)
        {
            AddDomainEvent(new CreditLowWarningEvent(Id, TenantId, currentBalance));
        }
    }

    private void RaiseDepletedEventIfNeeded(DateTime utcNow)
    {
        if (CalculateAvailableCredits(utcNow) +
            _creditLots
                .Where(lot => lot.ExpiresAt > utcNow)
                .Sum(lot => lot.ReservedCredits) == 0)
        {
            AddDomainEvent(new CreditDepletedEvent(Id, TenantId));
        }
    }
}