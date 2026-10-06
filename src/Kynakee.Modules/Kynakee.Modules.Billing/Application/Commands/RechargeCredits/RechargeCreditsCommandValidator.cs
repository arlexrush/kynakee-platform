using FluentValidation;

namespace Kynakee.Modules.Billing.Application.Commands.RechargeCredits;

public sealed class RechargeCreditsCommandValidator : AbstractValidator<RechargeCreditsCommand>
{
    public RechargeCreditsCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Source).IsInEnum();
        RuleFor(command => command.PurchasedAt)
            .Must(purchasedAt => purchasedAt.Kind == DateTimeKind.Utc);
        RuleFor(command => command.ExpiresAt)
            .Must(expiresAt => expiresAt.Kind == DateTimeKind.Utc)
            .GreaterThan(command => command.PurchasedAt.AddMonths(3));
    }
}