using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Billing.Application.Commands.ReleaseCredits;

public sealed class ReleaseCreditsCommandHandler
    : IRequestHandler<ReleaseCreditsCommand, Result>
{
    private readonly IBillingRepository _repository;

    public ReleaseCreditsCommandHandler(IBillingRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<Result> Handle(
        ReleaseCreditsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await _repository.GetAccountForUpdateAsync(
                request.TenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return ResultFactory.Failure(
                ApplicationError.NotFound(
                    "BILL_ACCOUNT_NOT_FOUND",
                    "The tenant credit account was not found."));
        }

        return account.Release(request.OperationId);
    }
}