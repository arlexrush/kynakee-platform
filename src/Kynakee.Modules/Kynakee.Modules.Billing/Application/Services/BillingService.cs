using Kynakee.Modules.Billing.Application.Commands.ConsumeCredits;
using Kynakee.Modules.Billing.Application.Commands.InitializeCreditAccount;
using Kynakee.Modules.Billing.Application.Commands.ReleaseCredits;
using Kynakee.Modules.Billing.Application.Commands.ReserveCredits;
using Kynakee.Modules.Billing.Application.Options;
using Kynakee.Modules.Billing.Application.Queries.GetCreditBalance;
using Kynakee.Modules.Billing.Application.Queries.GetCreditTransactions;
using Kynakee.Modules.Billing.Application.Queries.HasSufficientCredits;
using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using CreditReservationContract = Kynakee.Modules.Billing.Contracts.CreditReservation;

namespace Kynakee.Modules.Billing.Application.Services;

public sealed class BillingService : IBillingService, ITokenGateService
{
    private readonly ISender _sender;
    private readonly TimeProvider _timeProvider;
    private readonly BillingOptions _options;

    public BillingService(
        ISender sender,
        TimeProvider timeProvider,
        BillingOptions options)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            options.ReservationLifetime,
            TimeSpan.Zero);
        _sender = sender;
        _timeProvider = timeProvider;
        _options = options;
    }

    public async Task<Result<CreditReservationContract>> ReserveCreditsAsync(
        Guid tenantId,
        decimal amount,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var expiresAt = _timeProvider.GetUtcNow().Add(_options.ReservationLifetime).UtcDateTime;
        var result = await _sender.Send(
                new ReserveCreditsCommand(tenantId, amount, operationId, expiresAt),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? ResultFactory.Success(new CreditReservationContract(
                result.Value!.OperationId,
                result.Value.Amount,
                result.Value.ExpiresAt))
            : ResultFactory.Failure<CreditReservationContract>(result.Error!);
    }

    public Task<Result> ConsumeCreditsAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken) =>
        _sender.Send(new ConsumeCreditsCommand(tenantId, operationId), cancellationToken);

    public Task<Result> ReleaseCreditsAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken) =>
        _sender.Send(new ReleaseCreditsCommand(tenantId, operationId), cancellationToken);

    public Task<Result<CreditBalanceDto>> GetBalanceAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        _sender.Send(new GetCreditBalanceQuery(tenantId), cancellationToken);

    public Task<Result<bool>> HasSufficientCreditsAsync(
        Guid tenantId,
        decimal requiredAmount,
        CancellationToken cancellationToken) =>
        _sender.Send(new HasSufficientCreditsQuery(tenantId, requiredAmount), cancellationToken);

    public Task<Result<CreditTransactionHistoryDto>> GetTransactionsAsync(
        Guid tenantId,
        CreditTransactionKind? type,
        DateTime? from,
        DateTime? endDate,
        string? cursor,
        int limit,
        CancellationToken cancellationToken) =>
        _sender.Send(
            new GetCreditTransactionsQuery(
                tenantId,
                ToDomainTransactionType(type),
                from,
                endDate,
                cursor,
                limit),
            cancellationToken);

    private static CreditTransactionType? ToDomainTransactionType(
        CreditTransactionKind? type) =>
        type switch
        {
            null => null,
            CreditTransactionKind.Reserve => CreditTransactionType.Reservation,
            CreditTransactionKind.Consume => CreditTransactionType.Consumed,
            CreditTransactionKind.Release => CreditTransactionType.Released,
            CreditTransactionKind.Recharge => CreditTransactionType.Recharged,
            _ => null
        };

    public Task<Result> InitializeCreditAccountAsync(
        Guid tenantId,
        string planId,
        CancellationToken cancellationToken) =>
        _sender.Send(new InitializeCreditAccountCommand(tenantId, planId), cancellationToken);

    public async Task<Result> ReserveAsync(
        Guid tenantId,
        int creditsRequired,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var result = await ReserveCreditsAsync(
                tenantId,
                creditsRequired,
                operationId,
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? ResultFactory.Ok()
            : ResultFactory.Failure(result.Error!);
    }

    public Task<Result> ConsumeAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken) =>
        ConsumeCreditsAsync(tenantId, operationId, cancellationToken);

    public Task<Result> ReleaseAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken) =>
        ReleaseCreditsAsync(tenantId, operationId, cancellationToken);
}