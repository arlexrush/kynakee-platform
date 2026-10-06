using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the dependency injection container.")]
internal sealed class IdentityRepository : IIdentityRepository
{
    private readonly IdentityDbContext _dbContext;

    public IdentityRepository(IdentityDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<bool> TenantSlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        _dbContext.Tenants.IgnoreQueryFilters()
            .AnyAsync(tenant => tenant.Slug.Value == slug && !tenant.IsDeleted, cancellationToken);

    public Task<bool> TenantSlugExistsForOtherTenantAsync(
        string slug,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        _dbContext.Tenants.IgnoreQueryFilters()
            .AnyAsync(tenant => tenant.Id != tenantId && tenant.Slug.Value == slug && !tenant.IsDeleted,
                cancellationToken);

    public Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _dbContext.Tenants.SingleOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken);

    public Task<Tenant?> GetTenantForAuthenticationAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _dbContext.Tenants.IgnoreQueryFilters()
            .SingleOrDefaultAsync(tenant => tenant.Id == tenantId && !tenant.IsDeleted, cancellationToken);

    public Task<User?> GetUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _dbContext.Users.SingleOrDefaultAsync(
            user => user.TenantId == tenantId && user.Id == userId,
            cancellationToken);

    public Task<User?> GetUserForAuthenticationAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        _dbContext.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                user => user.TenantId == tenantId && user.Id == userId && !user.IsDeleted,
                cancellationToken);

    public Task<User?> FindUserForAuthenticationAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

    public Task<User?> FindUserByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        _dbContext.Users
            .SingleOrDefaultAsync(
                user => user.Email.Value == normalizedEmail,
                cancellationToken);

    public Task<User?> FindUserByEmailAcrossTenantsAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        _dbContext.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                user => user.Email.Value == normalizedEmail && !user.IsDeleted,
                cancellationToken);

    public Task<TenantUser?> GetTenantUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        _dbContext.TenantUsers.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId && membership.UserId == userId,
            cancellationToken);

    public Task<TenantUser?> GetTenantUserForAuthenticationAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                membership => membership.TenantId == tenantId && membership.UserId == userId && !membership.IsDeleted,
                cancellationToken);

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        _dbContext.RefreshTokens.IgnoreQueryFilters()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash && !token.IsDeleted, cancellationToken);

    public Task<UserInvitation?> FindInvitationByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        _dbContext.UserInvitations.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                invitation => invitation.TokenHash == tokenHash && !invitation.IsDeleted,
                cancellationToken);

    public Task<IdentityTenantProfile?> GetTenantProfileAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        _dbContext.Tenants.AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new IdentityTenantProfile(
                tenant.Id,
                tenant.Name,
                tenant.Slug.Value,
                tenant.Type,
                tenant.PlanId,
                tenant.Status,
                tenant.TaxId == null ? null : tenant.TaxId.Value,
                tenant.TaxId == null ? null : tenant.TaxId.CountryCode,
                new IdentityCompanySettings(
                    tenant.Settings.Administration,
                    tenant.Settings.Profit,
                    tenant.Settings.Quality,
                    tenant.Settings.SafetyHealth,
                    tenant.Settings.Environment,
                    tenant.Settings.Contingency),
                new IdentityCompanyBranding(
                    tenant.Branding.CompanyName,
                    tenant.Branding.PrimaryColor,
                    tenant.Branding.LogoUrl,
                    tenant.Branding.Email)))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<IdentityUserProfile?> GetUserProfileAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (from user in _dbContext.Users.AsNoTracking()
         join membership in _dbContext.TenantUsers.AsNoTracking()
             on new { user.TenantId, UserId = user.Id } equals new { membership.TenantId, membership.UserId }
         where user.TenantId == tenantId && user.Id == userId
         select new IdentityUserProfile(
             user.Id,
             user.TenantId,
             user.FirstName,
             user.LastName,
             user.Email.Value,
             user.Phone == null ? null : user.Phone.Value,
             membership.Role,
             user.Status))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TenantUserListItem>> GetTenantUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await (from user in _dbContext.Users.AsNoTracking()
               join membership in _dbContext.TenantUsers.AsNoTracking()
                   on new { user.TenantId, UserId = user.Id } equals new { membership.TenantId, membership.UserId }
               where user.TenantId == tenantId
               orderby user.LastName, user.FirstName
               select new TenantUserListItem(
                   user.Id,
                   user.FirstName,
                   user.LastName,
                   user.Email.Value,
                   user.Phone == null ? null : user.Phone.Value,
                   membership.Role,
                   user.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await _dbContext.RefreshTokens.IgnoreQueryFilters()
            .Where(token => token.TenantId == tenantId && token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken) =>
        (from membership in _dbContext.TenantUsers
         join user in _dbContext.Users on new { membership.TenantId, UserId = membership.UserId }
             equals new { user.TenantId, UserId = user.Id }
         where membership.TenantId == tenantId && membership.Role == UserRole.Owner && user.Status == UserStatus.Active
         select membership.Id).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<UserInvitation>> GetPendingInvitationsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await _dbContext.UserInvitations.IgnoreQueryFilters()
            .Where(invitation => invitation.TenantId == tenantId &&
                                 invitation.UserId == userId &&
                                 invitation.AcceptedAt == null &&
                                 invitation.RevokedAt == null &&
                                 !invitation.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void AddTenant(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        _dbContext.Tenants.Add(tenant);
    }

    public void AddUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _dbContext.Users.Add(user);
    }

    public void AddTenantUser(TenantUser membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        _dbContext.TenantUsers.Add(membership);
    }

    public void AddRefreshToken(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        _dbContext.RefreshTokens.Add(token);
    }

    public void AddInvitation(UserInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        _dbContext.UserInvitations.Add(invitation);
    }
}