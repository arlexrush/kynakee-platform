using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Queries.GetTenantProfile;

public sealed class GetTenantProfileQueryHandler : IRequestHandler<GetTenantProfileQuery, Result<IdentityTenantProfile>>
{
    private readonly IIdentityRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTenantProfileQueryHandler(IIdentityRepository repository, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IdentityTenantProfile>> Handle(
        GetTenantProfileQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsAuthenticated || request.TenantId != _tenantContext.TenantId ||
            request.RequestingUserId != _tenantContext.UserId)
        {
            return ResultFactory.Failure<IdentityTenantProfile>(ApplicationError.Unauthorized(
                "IDENTITY_TENANT_PROFILE_FORBIDDEN", "The current identity cannot access this tenant."));
        }

        var membership = await _repository.GetTenantUserAsync(
                request.TenantId,
                request.RequestingUserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (membership is null || membership.Role is not (UserRole.Owner or UserRole.Admin))
        {
            return ResultFactory.Failure<IdentityTenantProfile>(ApplicationError.Unauthorized(
                "IDENTITY_TENANT_PROFILE_FORBIDDEN", "Only tenant owners and administrators can view tenant details."));
        }

        var profile = await _repository.GetTenantProfileAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        return profile is null
            ? ResultFactory.Failure<IdentityTenantProfile>(ApplicationError.NotFound(
                "IDENTITY_TENANT_NOT_FOUND", "The tenant was not found."))
            : ResultFactory.Success(profile);
    }
}