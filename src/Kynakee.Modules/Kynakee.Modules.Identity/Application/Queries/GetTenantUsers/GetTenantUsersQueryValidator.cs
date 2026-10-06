using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Queries.GetTenantUsers;

public sealed class GetTenantUsersQueryValidator : AbstractValidator<GetTenantUsersQuery>
{
    public GetTenantUsersQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty();
        RuleFor(query => query.RequestingUserId).NotEmpty();
    }
}