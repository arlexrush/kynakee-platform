using System.Globalization;
using System.Resources;
using System.Diagnostics.CodeAnalysis;
using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;

namespace Kynakee.Modules.Identity.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the dependency injection container.")]
internal sealed class TenantManagementAuthorization(
    IIdentityRepository repository,
    ITenantContext tenantContext) : ITenantManagementAuthorization
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Identity.Infrastructure.TenantManagementResources", typeof(TenantManagementAuthorization).Assembly);

    public async Task<Result<bool>> AuthorizeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!tenantContext.IsAuthenticated || tenantContext.TenantId == Guid.Empty || tenantContext.UserId == Guid.Empty)
        {
            return Forbidden();
        }

        var user = await repository.GetUserProfileAsync(tenantContext.TenantId, tenantContext.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null || user.Id != tenantContext.UserId || user.TenantId != tenantContext.TenantId ||
            user.Status != UserStatus.Active || user.Role is not (UserRole.Owner or UserRole.Admin))
        {
            return Forbidden();
        }

        var tenant = await repository.GetTenantProfileAsync(tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        return tenant is not null && tenant.Id == tenantContext.TenantId && tenant.Status == TenantStatus.Active
            ? ResultFactory.Success(true)
            : Forbidden();
    }

    private static Result<bool> Forbidden() => ResultFactory.Failure<bool>(ApplicationError.Unauthorized(
        "IDENTITY_TENANT_MANAGEMENT_FORBIDDEN",
        Resources.GetString("Forbidden", CultureInfo.CurrentUICulture) ?? "Forbidden"));
}
