using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Billing.Application.Commands.RechargeCredits;

public sealed class RechargeCreditsCommandHandler
    : IRequestHandler<RechargeCreditsCommand, Result<CreditLotId>>
{
    private readonly IBillingRepository _repository;

    public RechargeCreditsCommandHandler(IBillingRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<Result<CreditLotId>> Handle(
        RechargeCreditsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await _repository.GetAccountForUpdateAsync(
                request.TenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return ResultFactory.Failure<CreditLotId>(
                ApplicationError.NotFound(
                    "BILL_ACCOUNT_NOT_FOUND",
                    "The tenant credit account was not found."));
        }

        return account.Recharge(
            request.Amount,
            request.Source,
            request.ExpiresAt,
            request.PurchasedAt,
            request.CreatedBy);
    }
}