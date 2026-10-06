using FluentValidation;

namespace Kynakee.Modules.Billing.Application.Commands.InitializeCreditAccount;

public sealed class InitializeCreditAccountCommandValidator
    : AbstractValidator<InitializeCreditAccountCommand>
{
    public InitializeCreditAccountCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.PlanId).NotEmpty().MaximumLength(100);
    }
}