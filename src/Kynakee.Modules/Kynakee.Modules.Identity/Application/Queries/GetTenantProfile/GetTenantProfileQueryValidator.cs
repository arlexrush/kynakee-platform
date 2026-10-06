using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Queries.GetTenantProfile;

public sealed class GetTenantProfileQueryValidator : AbstractValidator<GetTenantProfileQuery>
{
    public GetTenantProfileQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty();
        RuleFor(query => query.RequestingUserId).NotEmpty();
    }
}