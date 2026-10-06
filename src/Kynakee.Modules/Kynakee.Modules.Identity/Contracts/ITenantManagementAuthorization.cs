using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Contracts;

/// <summary>
/// Checks management permissions for the authenticated tenant without exposing Identity persistence.
/// </summary>
public interface ITenantManagementAuthorization
{
    /// <summary>
    /// Authorizes an active tenant owner or administrator in the current authenticated context.
    /// </summary>
    Task<Result<bool>> AuthorizeAsync(CancellationToken cancellationToken);
}
