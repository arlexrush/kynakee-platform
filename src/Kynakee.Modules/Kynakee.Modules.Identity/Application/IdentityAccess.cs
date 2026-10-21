using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Domain.ValueObjects;

namespace Kynakee.Modules.Identity.Application;

internal static class IdentityAccess
{
    internal static async Task<bool> HasActiveMembershipAsync(
        IIdentityRepository repository,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken,
        params UserRole[] allowedRoles)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        var user = await repository.GetUserProfileAsync(tenantId, userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null || user.Id != userId || user.TenantId != tenantId ||
            user.Status != UserStatus.Active ||
            (allowedRoles.Length > 0 && !allowedRoles.Contains(user.Role)))
        {
            return false;
        }

        var tenant = await repository.GetTenantProfileAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        return tenant is not null && tenant.Id == tenantId && tenant.Status == TenantStatus.Active;
    }
}
