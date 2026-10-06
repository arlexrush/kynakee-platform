using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public class JwtIdentityTokenServiceTests
{
    [Fact]
    public void CreateAccessTokenShouldBeSignedAndContainIdentityClaims()
    {
        var now = new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.Zero);
        var service = new JwtIdentityTokenService(CreateConfiguration(), new FixedTimeProvider(now));
        var tenant = Tenant.Create(
            "Kynakee Integration",
            TenantSlug.Create("kynakee-integration").Value!,
            TenantType.Company,
            null,
            null,
            "plan").Value!;
        var user = User.Create(
            tenant.Id,
            "Ada",
            "Lovelace",
            Email.Create("ada@example.test").Value!,
            null,
            "test-hash").Value!;
        var membership = TenantUser.Create(user, UserRole.Owner).Value!;

        var encodedToken = service.CreateAccessToken(user, tenant, membership);
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "test-issuer",
            ValidateAudience = true,
            ValidAudience = "test-audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 32))),
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                notBefore <= now.UtcDateTime && expires == now.AddMinutes(15).UtcDateTime,
            ClockSkew = TimeSpan.Zero
        };
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            encodedToken,
            validationParameters,
            out var validatedToken);
        var jwt = (JwtSecurityToken)validatedToken;

        jwt.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(user.Id.ToString("D"));
        principal.FindFirst("tenant_id")!.Value.Should().Be(tenant.Id.ToString("D"));
        principal.FindFirst(ClaimTypes.Email)!.Value.Should().Be(user.Email.Value);
        principal.FindFirst(ClaimTypes.Role)!.Value.Should().Be(nameof(UserRole.Owner));
        jwt.ValidTo.Should().Be(now.AddMinutes(15).UtcDateTime);
    }

    [Fact]
    public void GenerateOpaqueTokenShouldBeUniqueAndHashAsSha256()
    {
        var service = new JwtIdentityTokenService(CreateConfiguration(), TimeProvider.System);

        var first = service.GenerateOpaqueToken();
        var second = service.GenerateOpaqueToken();

        first.Should().NotBe(second);
        Convert.FromBase64String(first.Replace('-', '+').Replace('_', '/') + "==").Length.Should().Be(64);
        service.HashToken(first).Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first))));
    }

    [Fact]
    public void ConstructorShouldRejectSigningKeyShorterThanThirtyTwoBytes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SigningKey"] = "short"
            })
            .Build();

        var act = () => new JwtIdentityTokenService(configuration, TimeProvider.System);

        act.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SigningKey"] = new string('k', 32),
                ["Authentication:Issuer"] = "test-issuer",
                ["Authentication:Audience"] = "test-audience"
            })
            .Build();

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
