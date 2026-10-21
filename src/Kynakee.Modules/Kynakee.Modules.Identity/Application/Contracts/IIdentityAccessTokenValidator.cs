namespace Kynakee.Modules.Identity.Application.Contracts;

/// <summary>Checks signed token identity against the current account, membership and credential version.</summary>
public interface IIdentityAccessTokenValidator
{
    /// <summary>Rejects deleted or disabled accounts, outdated roles and credentials replaced by recovery.</summary>
    Task<bool> IsCurrentAsync(Guid tenantId, Guid userId, Guid credentialVersion, string role, CancellationToken cancellationToken);
}
