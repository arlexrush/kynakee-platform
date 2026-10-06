using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Billing.Domain.Entities;

public sealed class CreditLot : BaseEntity<CreditLotId>
{
    private CreditLot()
    {
    }

    private CreditLot(
        CreditLotId id,
        Guid tenantId,
        decimal amount,
        CreditSource source,
        DateTime purchasedAt,
        DateTime expiresAt,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        RemainingCredits = amount;
        AvailableCredits = amount;
        Source = source;
        PurchasedAt = purchasedAt;
        ExpiresAt = expiresAt;
    }

    public decimal RemainingCredits { get; private set; }

    public decimal AvailableCredits { get; private set; }

    public decimal ReservedCredits { get; private set; }

    public CreditSource Source { get; private set; }

    public DateTime PurchasedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    /// <summary>Creates a tenant-owned credit lot with at least three months of validity.</summary>
    public static Result<CreditLot> Create(
        Guid tenantId,
        decimal amount,
        CreditSource source,
        DateTime purchasedAt,
        DateTime expiresAt,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            return ResultFactory.Failure<CreditLot>(
                ApplicationError.Validation(
                    "BILL_TENANT_REQUIRED",
                    "The credit lot tenant is required."));
        }

        if (amount <= 0)
        {
            return ResultFactory.Failure<CreditLot>(
                ApplicationError.Validation(
                    "BILL_AMOUNT_INVALID",
                    "The credit lot amount must be greater than zero."));
        }

        if (!Enum.IsDefined(source))
        {
            return ResultFactory.Failure<CreditLot>(
                ApplicationError.Validation(
                    "BILL_SOURCE_INVALID",
                    "The credit source is invalid."));
        }

        if (purchasedAt.Kind != DateTimeKind.Utc ||
            expiresAt.Kind != DateTimeKind.Utc ||
            expiresAt < purchasedAt.AddMonths(3))
        {
            return ResultFactory.Failure<CreditLot>(
                ApplicationError.Validation(
                    "BILL_EXPIRY_TOO_EARLY",
                    "Credits must remain valid for at least three months after purchase."));
        }

        return ResultFactory.Success(
            new CreditLot(
                CreditLotId.New(),
                tenantId,
                amount,
                source,
                purchasedAt,
                expiresAt,
                createdBy));
    }

    public bool IsAvailableAt(DateTime utcNow) =>
        ExpiresAt > utcNow && AvailableCredits > 0;

    internal void Reserve(decimal amount, Guid? updatedBy)
    {
        AvailableCredits -= amount;
        ReservedCredits += amount;
        RegisterUpdate(updatedBy);
    }

    internal void ConsumeReservation(decimal amount, Guid? updatedBy)
    {
        ReservedCredits -= amount;
        RemainingCredits -= amount;
        RegisterUpdate(updatedBy);
    }

    internal void ReleaseReservation(decimal amount, Guid? updatedBy)
    {
        ReservedCredits -= amount;
        AvailableCredits += amount;
        RegisterUpdate(updatedBy);
    }
}