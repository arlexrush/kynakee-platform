using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Queries.GetCurrentIdentity;

public sealed class GetCurrentIdentityQueryHandler
    : IRequestHandler<GetCurrentIdentityQuery, Result<IdentitySessionProfile>>
{
    private readonly IIdentityRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCurrentIdentityQueryHandler(IIdentityRepository repository, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IdentitySessionProfile>> Handle(
        GetCurrentIdentityQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsAuthenticated || request.TenantId != _tenantContext.TenantId ||
            request.UserId != _tenantContext.UserId)
        {
            return ResultFactory.Failure<IdentitySessionProfile>(ApplicationError.Unauthorized(
                "IDENTITY_PROFILE_FORBIDDEN", "The current identity cannot access this profile."));
        }

        var user = await _repository.GetUserProfileAsync(request.TenantId, request.UserId, cancellationToken)
            .ConfigureAwait(false);
        var tenant = await _repository.GetTenantProfileAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null || tenant is null)
        {
            return ResultFactory.Failure<IdentitySessionProfile>(ApplicationError.NotFound(
                "IDENTITY_PROFILE_NOT_FOUND", "The identity profile was not found."));
        }

        return ResultFactory.Success(new IdentitySessionProfile(user, tenant));
    }
}