using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Billing.Application.Queries.HasSufficientCredits;

public sealed class HasSufficientCreditsQueryHandler
    : IRequestHandler<HasSufficientCreditsQuery, Result<bool>>
{
    private readonly IBillingRepository _repository;
    private readonly TimeProvider _timeProvider;

    public HasSufficientCreditsQueryHandler(
        IBillingRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<bool>> Handle(
        HasSufficientCreditsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var balance = await _repository.GetBalanceAsync(
                request.TenantId,
                _timeProvider.GetUtcNow().UtcDateTime,
                cancellationToken)
            .ConfigureAwait(false);
        return balance is null
            ? ResultFactory.Failure<bool>(
                ApplicationError.NotFound(
                    "BILL_ACCOUNT_NOT_FOUND",
                    "The tenant credit account was not found."))
            : ResultFactory.Success(balance.AvailableCredits >= request.RequiredAmount);
    }
}