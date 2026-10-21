using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Identity.Infrastructure;

internal sealed class IdentityAccessTokenValidator(IdentityDbContext context) : IIdentityAccessTokenValidator
{
    public Task<bool> IsCurrentAsync(Guid tenantId, Guid userId, Guid credentialVersion, string role, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || credentialVersion == Guid.Empty ||
            !Enum.TryParse<UserRole>(role, out var expectedRole) || !Enum.IsDefined(expectedRole))
            return Task.FromResult(false);

        return (from user in context.Users.IgnoreQueryFilters().AsNoTracking()
                join tenant in context.Tenants.IgnoreQueryFilters() on user.TenantId equals tenant.Id
                join membership in context.TenantUsers.IgnoreQueryFilters() on user.Id equals membership.UserId
                where user.Id == userId && user.TenantId == tenantId && !user.IsDeleted && user.Status == UserStatus.Active
                    && user.CredentialVersion == credentialVersion
                    && tenant.Id == tenantId && !tenant.IsDeleted && tenant.Status == TenantStatus.Active
                    && membership.TenantId == tenantId && !membership.IsDeleted && membership.Role == expectedRole
                select user.Id).AnyAsync(cancellationToken);
    }
}
