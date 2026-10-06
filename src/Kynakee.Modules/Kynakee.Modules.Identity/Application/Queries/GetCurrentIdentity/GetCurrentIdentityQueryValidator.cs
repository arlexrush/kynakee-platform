using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Queries.GetCurrentIdentity;

public sealed class GetCurrentIdentityQueryValidator : AbstractValidator<GetCurrentIdentityQuery>
{
    public GetCurrentIdentityQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty();
        RuleFor(query => query.UserId).NotEmpty();
    }
}