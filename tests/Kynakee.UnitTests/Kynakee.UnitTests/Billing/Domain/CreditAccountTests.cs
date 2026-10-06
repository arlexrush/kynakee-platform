using FluentAssertions;
using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.Events;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Billing.Domain;

public class CreditAccountTests
{
    private static readonly DateTime Now =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateShouldRejectEmptyTenantId()
    {
        var result = CreditAccount.Create(Guid.Empty, CreatePlanId());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_TENANT_REQUIRED");
    }

    [Fact]
    public void ReserveShouldAllocateFromEarliestExpiringLotsFirst()
    {
        var account = CreateAccount();
        var laterExpiringLot = Recharge(account, 100, Now.AddMonths(6));
        var soonerExpiringLot = Recharge(account, 40, Now.AddMonths(3));

        var result = account.Reserve(50, Guid.NewGuid(), Now.AddMinutes(10), Now);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Allocations.Should().ContainInOrder(
            new CreditReservationAllocation(soonerExpiringLot, 40),
            new CreditReservationAllocation(laterExpiringLot, 10));
        account.CalculateAvailableCredits(Now).Should().Be(90);
        account.ReservedCredits.Should().Be(50);
    }

    [Fact]
    public void ReserveShouldLimitReservationExpiryToEarliestAllocatedLot()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));

        var result = account.Reserve(25, Guid.NewGuid(), Now.AddMonths(4), Now);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExpiresAt.Should().Be(Now.AddMonths(3));
    }

    [Fact]
    public void ReserveShouldRejectInsufficientUnexpiredCredits()
    {
        var account = CreateAccount();
        Recharge(account, 50, Now.AddMonths(3));

        var result = account.Reserve(51, Guid.NewGuid(), Now.AddMinutes(10), Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_INSUFFICIENT_CREDITS");
        account.CalculateAvailableCredits(Now).Should().Be(50);
        account.ReservedCredits.Should().Be(0);
    }

    [Fact]
    public void ReserveShouldReturnExistingReservationForDuplicateOperation()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));
        var operationId = Guid.NewGuid();
        var first = account.Reserve(30, operationId, Now.AddMinutes(10), Now);

        var replay = account.Reserve(30, operationId, Now.AddMinutes(20), Now);

        replay.IsSuccess.Should().BeTrue();
        replay.Value.Should().BeEquivalentTo(first.Value);
        account.CalculateAvailableCredits(Now).Should().Be(70);
        account.Transactions.Should().ContainSingle(
            transaction => transaction.Type == CreditTransactionType.Reservation);
    }

    [Fact]
    public void ReserveShouldRejectDuplicateOperationWithDifferentAmount()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));
        var operationId = Guid.NewGuid();
        account.Reserve(30, operationId, Now.AddMinutes(10), Now);

        var result = account.Reserve(31, operationId, Now.AddMinutes(10), Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_OPERATION_FINALIZED");
        account.CalculateAvailableCredits(Now).Should().Be(70);
    }

    [Fact]
    public void ReserveShouldIgnoreExpiredCreditLots()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));

        var result = account.Reserve(
            1,
            Guid.NewGuid(),
            Now.AddMonths(4).AddMinutes(10),
            Now.AddMonths(4));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_INSUFFICIENT_CREDITS");
        account.CalculateAvailableCredits(Now.AddMonths(4)).Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ReserveExpiredOrCurrentExpiryShouldReturnValidationError(int expiryOffsetMinutes)
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));

        var result = account.Reserve(
            1,
            Guid.NewGuid(),
            Now.AddMinutes(expiryOffsetMinutes),
            Now);

        result.Error!.Code.Should().Be("BILL_RESERVATION_EXPIRY_INVALID");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ReserveExpiredOrCurrentExpiryShouldLeaveBalanceUnchanged(int expiryOffsetMinutes)
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));

        account.Reserve(1, Guid.NewGuid(), Now.AddMinutes(expiryOffsetMinutes), Now);

        account.CalculateAvailableCredits(Now).Should().Be(100);
    }

    [Fact]
    public void ConsumeShouldDeductReservedCreditsFromTheirOriginalLots()
    {
        var account = CreateAccount();
        var lotId = Recharge(account, 100, Now.AddMonths(3));
        var operationId = Guid.NewGuid();
        account.Reserve(40, operationId, Now.AddMinutes(10), Now);

        var result = account.Consume(operationId, Now.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        account.CreditLots.Single(lot => lot.Id == lotId).RemainingCredits.Should().Be(60);
        account.CalculateAvailableCredits(Now).Should().Be(60);
        account.ReservedCredits.Should().Be(0);
        account.DomainEvents.Should().ContainSingle(
            domainEvent => domainEvent is CreditsConsumedEvent);
    }

    [Fact]
    public void ConsumeShouldBeIdempotentForAlreadyConsumedOperation()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));
        var operationId = Guid.NewGuid();
        account.Reserve(30, operationId, Now.AddMinutes(10), Now);
        account.Consume(operationId, Now.AddMinutes(1));
        var eventCount = account.DomainEvents.Count;

        var replay = account.Consume(operationId, Now.AddMinutes(2));

        replay.IsSuccess.Should().BeTrue();
        account.DomainEvents.Should().HaveCount(eventCount);
        account.CalculateAvailableCredits(Now).Should().Be(70);
    }

    [Fact]
    public void ReleaseShouldReturnReservationToItsLots()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));
        var operationId = Guid.NewGuid();
        account.Reserve(30, operationId, Now.AddMinutes(10), Now);

        var result = account.Release(operationId);

        result.IsSuccess.Should().BeTrue();
        account.CalculateAvailableCredits(Now).Should().Be(100);
        account.ReservedCredits.Should().Be(0);
    }

    [Fact]
    public void ReleaseShouldBeIdempotentForAlreadyReleasedOperation()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));
        var operationId = Guid.NewGuid();
        account.Reserve(30, operationId, Now.AddMinutes(10), Now);
        account.Release(operationId);
        var eventCount = account.DomainEvents.Count;

        var replay = account.Release(operationId);

        replay.IsSuccess.Should().BeTrue();
        account.DomainEvents.Should().HaveCount(eventCount);
        account.CalculateAvailableCredits(Now).Should().Be(100);
    }

    [Fact]
    public void ConsumeShouldRejectExpiredReservation()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));
        var operationId = Guid.NewGuid();
        account.Reserve(30, operationId, Now.AddMinutes(10), Now);

        var result = account.Consume(operationId, Now.AddMinutes(11));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_RESERVATION_EXPIRED");
        account.CreditLots.Single().ReservedCredits.Should().Be(30);
    }

    [Fact]
    public void ReserveShouldRaiseLowCreditEventWhenBalanceCrossesBelowFifty()
    {
        var account = CreateAccount();
        Recharge(account, 100, Now.AddMonths(3));

        account.Reserve(51, Guid.NewGuid(), Now.AddMinutes(10), Now);

        account.DomainEvents.Should().ContainSingle(
            domainEvent => domainEvent is CreditLowWarningEvent);
    }

    [Fact]
    public void ConsumeShouldRaiseDepletedEventWhenNoUnexpiredCreditsRemain()
    {
        var account = CreateAccount();
        Recharge(account, 25, Now.AddMonths(3));
        var operationId = Guid.NewGuid();
        account.Reserve(25, operationId, Now.AddMinutes(10), Now);

        account.Consume(operationId, Now.AddMinutes(1));

        account.DomainEvents.Should().ContainSingle(
            domainEvent => domainEvent is CreditDepletedEvent);
    }

    [Fact]
    public void RechargeShouldRejectExpiryBeforeThreeMonths()
    {
        var account = CreateAccount();

        var result = account.Recharge(
            100,
            CreditSource.Stripe,
            Now.AddMonths(3).AddTicks(-1),
            Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BILL_EXPIRY_TOO_EARLY");
        account.CreditLots.Should().BeEmpty();
    }

    private static CreditAccount CreateAccount() =>
        CreditAccount.Create(Guid.NewGuid(), CreatePlanId()).Value!;

    private static PlanId CreatePlanId() =>
        PlanId.Create("starter").Value;

    private static CreditLotId Recharge(
        CreditAccount account,
        decimal amount,
        DateTime expiresAt) =>
        account.Recharge(
            amount,
            CreditSource.Stripe,
            expiresAt,
            Now).Value;
}