using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Billing.Domain.Entities;

public sealed class CreditTransaction : BaseEntity<Guid>
{
    private readonly List<CreditReservationAllocation> _allocations = [];

    private CreditTransaction()
    {
    }

    private CreditTransaction(
        Guid id,
        Guid tenantId,
        CreditTransactionType type,
        decimal amount,
        Guid? operationId,
        CreditSource? source,
        DateTime? expiresAt,
        IReadOnlyCollection<CreditReservationAllocation> allocations,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        Type = type;
        Amount = amount;
        OperationId = operationId;
        Source = source;
        ExpiresAt = expiresAt;
        _allocations.AddRange(allocations);
    }

    public CreditTransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public Guid? OperationId { get; private set; }

    public CreditSource? Source { get; private set; }

    public DateTime? ExpiresAt { get; private set; }

    public IReadOnlyList<CreditReservationAllocation> Allocations =>
        _allocations.AsReadOnly();

    internal static CreditTransaction Create(
        Guid tenantId,
        CreditTransactionType type,
        decimal amount,
        Guid? operationId,
        CreditSource? source,
        DateTime? expiresAt,
        IReadOnlyCollection<CreditReservationAllocation> allocations,
        Guid? createdBy) =>
        new(
            Guid.NewGuid(),
            tenantId,
            type,
            amount,
            operationId,
            source,
            expiresAt,
            allocations,
            createdBy);
}