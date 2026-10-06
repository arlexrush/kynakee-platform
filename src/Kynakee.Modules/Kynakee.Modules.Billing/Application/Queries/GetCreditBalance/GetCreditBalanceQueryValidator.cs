using FluentValidation;

namespace Kynakee.Modules.Billing.Application.Queries.GetCreditBalance;

public sealed class GetCreditBalanceQueryValidator : AbstractValidator<GetCreditBalanceQuery>
{
    public GetCreditBalanceQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty();
    }
}