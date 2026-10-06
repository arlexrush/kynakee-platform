using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Billing.Application.Queries.GetCreditBalance;

public sealed class GetCreditBalanceQueryHandler
    : IRequestHandler<GetCreditBalanceQuery, Result<CreditBalanceDto>>
{
    private readonly IBillingRepository _repository;
    private readonly TimeProvider _timeProvider;

    public GetCreditBalanceQueryHandler(
        IBillingRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CreditBalanceDto>> Handle(
        GetCreditBalanceQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var balance = await _repository.GetBalanceAsync(
                request.TenantId,
                _timeProvider.GetUtcNow().UtcDateTime,
                cancellationToken)
            .ConfigureAwait(false);
        return balance is null
            ? ResultFactory.Failure<CreditBalanceDto>(
                ApplicationError.NotFound(
                    "BILL_ACCOUNT_NOT_FOUND",
                    "The tenant credit account was not found."))
            : ResultFactory.Success(new CreditBalanceDto(
                balance.AvailableCredits,
                balance.ReservedCredits,
                balance.PlanId,
                balance.NextRenewalDate));
    }
}