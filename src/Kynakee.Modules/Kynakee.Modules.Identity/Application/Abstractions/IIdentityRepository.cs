using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;

namespace Kynakee.Modules.Identity.Application.Abstractions;

public interface IIdentityRepository
{
    Task<bool> TenantSlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<bool> TenantSlugExistsForOtherTenantAsync(
        string slug,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Tenant?> GetTenantForAuthenticationAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<User?> GetUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<User?> GetUserForAuthenticationAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<User?> FindUserForAuthenticationAsync(Guid userId, CancellationToken cancellationToken);

    Task<User?> FindUserByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<User?> FindUserByEmailAcrossTenantsAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<TenantUser?> GetTenantUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<TenantUser?> GetTenantUserForAuthenticationAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<RefreshToken?> FindRefreshTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task<UserInvitation?> FindInvitationByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task<IdentityTenantProfile?> GetTenantProfileAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IdentityUserProfile?> GetUserProfileAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantUserListItem>> GetTenantUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserInvitation>> GetPendingInvitationsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    void AddTenant(Tenant tenant);

    void AddUser(User user);

    void AddTenantUser(TenantUser membership);

    void AddRefreshToken(RefreshToken token);

    void AddInvitation(UserInvitation invitation);
}
