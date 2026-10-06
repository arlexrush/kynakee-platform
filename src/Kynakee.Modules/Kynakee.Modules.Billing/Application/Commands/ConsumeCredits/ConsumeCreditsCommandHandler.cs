using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Billing.Application.Commands.ConsumeCredits;

public sealed class ConsumeCreditsCommandHandler
    : IRequestHandler<ConsumeCreditsCommand, Result>
{
    private readonly IBillingRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ConsumeCreditsCommandHandler(
        IBillingRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        ConsumeCreditsCommand request,
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

        return account.Consume(
            request.OperationId,
            _timeProvider.GetUtcNow().UtcDateTime);
    }
}