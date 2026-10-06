using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.DeactivateTenantUser;

public sealed class DeactivateTenantUserCommandHandler : IRequestHandler<DeactivateTenantUserCommand, Result>
{
    private readonly IIdentityRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public DeactivateTenantUserCommandHandler(
        IIdentityRepository repository,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(DeactivateTenantUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsAuthenticated || request.TenantId != _tenantContext.TenantId)
        {
            return Forbidden();
        }

        var actor = await _repository.GetTenantUserAsync(
                request.TenantId,
                _tenantContext.UserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (actor is null || actor.Role != UserRole.Owner)
        {
            return Forbidden();
        }

        var target = await _repository.GetTenantUserAsync(request.TenantId, request.UserId, cancellationToken)
            .ConfigureAwait(false);
        var user = await _repository.GetUserAsync(request.TenantId, request.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (target is null || user is null)
        {
            return ResultFactory.Failure(ApplicationError.NotFound(
                "IDENTITY_TENANT_USER_NOT_FOUND", "The tenant user was not found."));
        }

        if (target.Role == UserRole.Owner && user.Status == UserStatus.Active &&
            await _repository.CountActiveOwnersAsync(request.TenantId, cancellationToken).ConfigureAwait(false) <= 1)
        {
            return ResultFactory.Failure(ApplicationError.Conflict(
                "IDENTITY_LAST_OWNER_REQUIRED", "A tenant must have at least one active owner."));
        }

        var deactivateResult = user.Deactivate(_tenantContext.UserId);
        if (deactivateResult.IsFailure)
        {
            return deactivateResult;
        }

        foreach (var refreshToken in await _repository.GetActiveRefreshTokensAsync(
                     request.TenantId,
                     request.UserId,
                     cancellationToken).ConfigureAwait(false))
        {
            var revokeResult = refreshToken.Revoke(_timeProvider.GetUtcNow(), updatedBy: _tenantContext.UserId);
            if (revokeResult.IsFailure)
            {
                return revokeResult;
            }
        }

        foreach (var invitation in await _repository.GetPendingInvitationsAsync(
                     request.TenantId,
                     request.UserId,
                     cancellationToken).ConfigureAwait(false))
        {
            var revokeResult = invitation.Revoke(_timeProvider.GetUtcNow(), _tenantContext.UserId);
            if (revokeResult.IsFailure)
            {
                return revokeResult;
            }
        }

        return target.DeleteMembership(_tenantContext.UserId);
    }

    private static Result Forbidden() =>
        ResultFactory.Failure(ApplicationError.Unauthorized(
            "IDENTITY_USER_DEACTIVATION_FORBIDDEN", "Only tenant owners can deactivate users."));
}