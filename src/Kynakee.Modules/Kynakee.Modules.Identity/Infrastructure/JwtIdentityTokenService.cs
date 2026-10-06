using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Kynakee.Modules.Identity.Infrastructure;

internal sealed class JwtIdentityTokenService : IIdentityTokenService
{
    private const int TokenBytes = 64;
    private readonly byte[] _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeProvider _timeProvider;

    public JwtIdentityTokenService(IConfiguration configuration, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var signingKey = configuration["Authentication:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Authentication:SigningKey must be configured with at least 32 UTF-8 bytes.");
        }

        _signingKey = Encoding.UTF8.GetBytes(signingKey);
        _issuer = configuration["Authentication:Issuer"] ?? "Kynakee";
        _audience = configuration["Authentication:Audience"] ?? "KynakeeClients";
        _timeProvider = timeProvider;
    }

    public string CreateAccessToken(User user, Tenant tenant, TenantUser membership)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(membership);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            new Claim("tenant_id", tenant.Id.ToString("D")),
            new Claim(ClaimTypes.Email, user.Email.Value),
            new Claim(ClaimTypes.Role, membership.Role.ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_signingKey),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _issuer,
            _audience,
            claims,
            now,
            now.AddSeconds(IdentitySessionLifetime.AccessTokenSeconds),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateOpaqueToken() =>
        Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    public string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}

internal static class IdentitySessionLifetime
{
    internal const int AccessTokenSeconds = 900;
}
