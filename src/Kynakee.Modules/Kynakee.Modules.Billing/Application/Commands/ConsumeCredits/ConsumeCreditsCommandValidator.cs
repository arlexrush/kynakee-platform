using FluentValidation;

namespace Kynakee.Modules.Billing.Application.Commands.ConsumeCredits;

public sealed class ConsumeCreditsCommandValidator : AbstractValidator<ConsumeCreditsCommand>
{
    public ConsumeCreditsCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.OperationId).NotEmpty();
    }
}