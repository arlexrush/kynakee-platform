using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Billing.Application.Commands.ReserveCredits;

public sealed class ReserveCreditsCommandHandler
    : IRequestHandler<ReserveCreditsCommand, Result<CreditReservation>>
{
    private readonly IBillingRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ReserveCreditsCommandHandler(
        IBillingRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CreditReservation>> Handle(
        ReserveCreditsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await _repository.GetAccountForUpdateAsync(
                request.TenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return ResultFactory.Failure<CreditReservation>(
                ApplicationError.NotFound(
                    "BILL_ACCOUNT_NOT_FOUND",
                    "The tenant credit account was not found."));
        }

        return account.Reserve(
            request.Amount,
            request.OperationId,
            request.ExpiresAt,
            _timeProvider.GetUtcNow().UtcDateTime);
    }
}