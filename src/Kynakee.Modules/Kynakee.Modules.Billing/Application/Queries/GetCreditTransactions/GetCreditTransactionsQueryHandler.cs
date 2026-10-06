using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Billing.Application.Queries.GetCreditTransactions;

public sealed class GetCreditTransactionsQueryHandler
    : IRequestHandler<GetCreditTransactionsQuery, Result<CreditTransactionHistoryDto>>
{
    private readonly IBillingRepository _repository;

    public GetCreditTransactionsQueryHandler(IBillingRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<Result<CreditTransactionHistoryDto>> Handle(
        GetCreditTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = await _repository.GetTransactionsAsync(
                request.TenantId,
                request.Type,
                request.From,
                request.EndDate,
                request.Cursor,
                request.Limit,
                cancellationToken)
            .ConfigureAwait(false);

        return ResultFactory.Success(
            new CreditTransactionHistoryDto(
                page.Items.Select(item => new CreditTransactionHistoryItem(
                    item.Id,
                    item.OperationId,
                    ToContractTransactionType(item.Type),
                    item.Amount,
                    ToContractSource(item.Source),
                    item.CreatedAt,
                    item.ExpiresAt)).ToArray(),
                page.NextCursor));
    }

    private static CreditTransactionKind ToContractTransactionType(
        CreditTransactionType type) =>
        type switch
        {
            CreditTransactionType.Reservation => CreditTransactionKind.Reserve,
            CreditTransactionType.Consumed => CreditTransactionKind.Consume,
            CreditTransactionType.Released => CreditTransactionKind.Release,
            CreditTransactionType.Recharged => CreditTransactionKind.Recharge,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

    private static string? ToContractSource(CreditSource? source) =>
        source switch
        {
            null => null,
            CreditSource.Stripe => "stripe",
            CreditSource.Manual => "manual",
            CreditSource.Promotional => "promo",
            CreditSource.Subscription => "subscription",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
}