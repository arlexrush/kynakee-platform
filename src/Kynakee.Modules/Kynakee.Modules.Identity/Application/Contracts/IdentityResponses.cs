using Kynakee.Modules.Identity.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace Kynakee.Modules.Identity.Application.Contracts;

public sealed record IdentitySessionResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType,
    IdentityUserProfile User,
    IdentityTenantProfile Tenant);

public sealed record IdentityTenantProfile(
    Guid Id,
    string Name,
    string Slug,
    TenantType Type,
    string PlanId,
    TenantStatus Status,
    string? TaxId,
    string? TaxCountry,
    IdentityCompanySettings Settings,
    IdentityCompanyBranding Branding)
{
    /// <summary>The tenant's fiscal address, when supplied.</summary>
    public IdentityFiscalAddress? FiscalAddress { get; init; }

    [JsonIgnore]
    public uint Version { get; init; }
}

public sealed record IdentityFiscalAddress(
    string Country,
    string? Region,
    string? Province,
    string? Municipality,
    string? PostalCode,
    string? Street);

public sealed record IdentityCompanySettings(
    decimal Administration,
    decimal Profit,
    decimal Quality,
    decimal SafetyHealth,
    decimal Environment,
    decimal Contingency);

public sealed record IdentityCompanyBranding(
    string? CompanyName,
    string? PrimaryColor,
    Uri? LogoUrl,
    string? Email);

public sealed record IdentityUserProfile(
    Guid Id,
    Guid TenantId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    UserRole Role,
    UserStatus Status)
{
    [JsonIgnore]
    public uint Version { get; init; }
}

public sealed record TenantUserListItem(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    UserRole Role,
    UserStatus Status)
{
    [JsonIgnore]
    public uint MembershipVersion { get; init; }

    public string MembershipETag { get; init; } = string.Empty;
}

public sealed record InvitationCreatedResponse(Guid InvitationId, DateTimeOffset ExpiresAt);