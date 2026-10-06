using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands;

internal static class IdentitySessionFactory
{
    internal const int AccessTokenLifetimeSeconds = 900;

    internal static Result<IdentitySessionResponse> Create(
        User user,
        Tenant tenant,
        TenantUser membership,
        IIdentityRepository repository,
        IIdentityTokenService tokenService,
        DateTimeOffset now)
    {
        var refreshTokenValue = tokenService.GenerateOpaqueToken();
        var refreshToken = RefreshToken.Create(
            user.TenantId,
            user.Id,
            tokenService.HashToken(refreshTokenValue),
            now);
        if (refreshToken.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(refreshToken.Error!);
        }

        repository.AddRefreshToken(refreshToken.Value!);
        var accessToken = tokenService.CreateAccessToken(user, tenant, membership);
        return ResultFactory.Success(new IdentitySessionResponse(
            accessToken,
            refreshTokenValue,
            AccessTokenLifetimeSeconds,
            "Bearer",
            IdentitySessionFactory.MapUser(user, membership),
            IdentitySessionFactory.MapTenant(tenant)));
    }

    internal static IdentityTenantProfile MapTenant(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Slug.Value,
        tenant.Type,
        tenant.PlanId,
        tenant.Status,
        tenant.TaxId?.Value,
        tenant.TaxId?.CountryCode,
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
            tenant.Branding.Email));

    internal static IdentityUserProfile MapUser(User user, TenantUser membership) => new(
        user.Id,
        user.TenantId,
        user.FirstName,
        user.LastName,
        user.Email.Value,
        user.Phone?.Value,
        membership.Role,
        user.Status);
}