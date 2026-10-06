using FluentValidation;

namespace Kynakee.Modules.Billing.Application.Commands.ReleaseCredits;

public sealed class ReleaseCreditsCommandValidator : AbstractValidator<ReleaseCreditsCommand>
{
    public ReleaseCreditsCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.OperationId).NotEmpty();
    }
}