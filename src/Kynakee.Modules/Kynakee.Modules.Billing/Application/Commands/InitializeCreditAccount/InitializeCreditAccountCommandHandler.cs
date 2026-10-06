using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.Billing.Application.Plans;
using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Billing.Application.Commands.InitializeCreditAccount;

public sealed class InitializeCreditAccountCommandHandler
    : IRequestHandler<InitializeCreditAccountCommand, Result>
{
    private readonly IBillingRepository _repository;
    private readonly TimeProvider _timeProvider;

    public InitializeCreditAccountCommandHandler(
        IBillingRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        InitializeCreditAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!BillingPlanCatalog.TryGet(request.PlanId, out var plan))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation(
                    "BILL_PLAN_UNKNOWN",
                    "The requested billing plan is not available."));
        }

        var existingAccount = await _repository.GetAccountForUpdateAsync(
                request.TenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingAccount is not null)
        {
            return string.Equals(
                existingAccount.PlanId.Value,
                plan!.Id,
                StringComparison.OrdinalIgnoreCase)
                ? ResultFactory.Ok()
                : ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "BILL_ACCOUNT_ALREADY_INITIALIZED",
                        "The tenant already has a credit account for a different plan."));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var planId = PlanId.Create(plan!.Id).Value;
        var accountResult = CreditAccount.Create(request.TenantId, planId);
        if (accountResult.IsFailure)
        {
            return ResultFactory.Failure(accountResult.Error!);
        }

        var account = accountResult.Value!;
        var subscriptionResult = Subscription.Create(
            request.TenantId,
            planId,
            BillingCycle.Monthly,
            now,
            now.AddMonths(1),
            plan.CreditsPerCycle);
        if (subscriptionResult.IsFailure)
        {
            return ResultFactory.Failure(subscriptionResult.Error!);
        }

        var rechargeResult = account.Recharge(
            plan.CreditsPerCycle,
            CreditSource.Subscription,
            now.AddMonths(3),
            now);
        if (rechargeResult.IsFailure)
        {
            return ResultFactory.Failure(rechargeResult.Error!);
        }

        _repository.AddAccount(account);
        _repository.AddSubscription(subscriptionResult.Value!);

        return ResultFactory.Ok();
    }
}