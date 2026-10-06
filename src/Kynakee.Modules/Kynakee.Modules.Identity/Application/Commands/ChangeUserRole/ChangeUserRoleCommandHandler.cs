using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.ChangeUserRole;

public sealed class ChangeUserRoleCommandHandler : IRequestHandler<ChangeUserRoleCommand, Result>
{
    private readonly IIdentityRepository _repository;
    private readonly ITenantContext _tenantContext;

    public ChangeUserRoleCommandHandler(IIdentityRepository repository, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Result> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsAuthenticated || request.TenantId != _tenantContext.TenantId)
        {
            return Forbidden();
        }

        var actorMembership = await _repository.GetTenantUserAsync(
                request.TenantId,
                _tenantContext.UserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (actorMembership is null || actorMembership.Role != UserRole.Owner)
        {
            return Forbidden();
        }

        var target = await _repository.GetTenantUserAsync(request.TenantId, request.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (target is null)
        {
            return ResultFactory.Failure(ApplicationError.NotFound(
                "IDENTITY_TENANT_USER_NOT_FOUND", "The tenant user was not found."));
        }

        if (target.Role == UserRole.Owner && request.Role != UserRole.Owner &&
            await _repository.CountActiveOwnersAsync(request.TenantId, cancellationToken).ConfigureAwait(false) <= 1)
        {
            return ResultFactory.Failure(ApplicationError.Conflict(
                "IDENTITY_LAST_OWNER_REQUIRED", "A tenant must have at least one active owner."));
        }

        return target.ChangeRole(request.Role, _tenantContext.UserId);
    }

    private static Result Forbidden() =>
        ResultFactory.Failure(ApplicationError.Unauthorized(
            "IDENTITY_ROLE_CHANGE_FORBIDDEN", "Only tenant owners can change user roles."));
}