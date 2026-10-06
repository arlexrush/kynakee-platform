using FluentValidation;

namespace Kynakee.Modules.Billing.Application.Queries.HasSufficientCredits;

public sealed class HasSufficientCreditsQueryValidator
    : AbstractValidator<HasSufficientCreditsQuery>
{
    public HasSufficientCreditsQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty();
        RuleFor(query => query.RequiredAmount).GreaterThan(0);
    }
}