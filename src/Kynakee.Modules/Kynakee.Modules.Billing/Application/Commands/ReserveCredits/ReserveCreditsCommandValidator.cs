using FluentValidation;

namespace Kynakee.Modules.Billing.Application.Commands.ReserveCredits;

public sealed class ReserveCreditsCommandValidator : AbstractValidator<ReserveCreditsCommand>
{
    public ReserveCreditsCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.OperationId).NotEmpty();
        RuleFor(command => command.ExpiresAt)
            .Must(expiresAt => expiresAt.Kind == DateTimeKind.Utc);
    }
}