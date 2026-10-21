using Kynakee.Api.Endpoints;
using Kynakee.Api.Infrastructure.Identity;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.RateLimiting;
using NSubstitute;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class IdentityHttpIdempotencyEndpointTests : IAsyncLifetime
{
    private WebApplication _app = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("identity-auth", limiter =>
        {
            limiter.PermitLimit = 100;
            limiter.Window = TimeSpan.FromMinutes(1);
        }));
        builder.Services.AddSingleton(Substitute.For<ISender>());
        builder.Services.AddSingleton(new IdentityEntityTagProtector(new string('e', 32)));
        builder.Services.AddSingleton<IHttpIdempotencyProtector>(new IdentityHttpIdempotencyProtector(new string('i', 32)));

        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("tenant_id", Guid.NewGuid().ToString()),
                    new Claim("sub", Guid.NewGuid().ToString())
                ],
                "Test"));
            await next(context).ConfigureAwait(false);
        });
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.MapIdentityEndpoints();
        await _app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync().ConfigureAwait(true);

    [Theory]
    [InlineData("/api/v1/identity/auth/register", "{\"firstName\":\"Test\",\"lastName\":\"User\",\"email\":\"user@example.test\",\"password\":\"Password!123\",\"phone\":\"+12025550123\",\"tenantName\":\"Tenant\",\"tenantType\":0,\"taxId\":\"123\",\"taxCountry\":\"US\",\"fiscalCountry\":null,\"fiscalRegion\":null,\"fiscalProvince\":null,\"fiscalMunicipality\":null,\"fiscalPostalCode\":null,\"fiscalStreet\":null,\"planId\":\"starter\",\"channel\":\"web\"}")]
    [InlineData("/api/v1/identity/auth/login", "{\"email\":\"user@example.test\",\"password\":\"Password!123\",\"channel\":\"web\"}")]
    [InlineData("/api/v1/identity/auth/refresh", "{\"refreshToken\":\"token\"}")]
    [InlineData("/api/v1/identity/auth/invitations/accept", "{\"invitationToken\":\"token\",\"password\":\"Password!123\"}")]
    [InlineData("/api/v1/identity/users/invite", "{\"firstName\":\"Test\",\"lastName\":\"User\",\"email\":\"user@example.test\",\"phone\":null,\"role\":0}")]
    [InlineData("/api/v1/identity/auth/email/resend", "{\"email\":\"user@example.test\"}")]
    [InlineData("/api/v1/identity/auth/password/forgot", "{\"email\":\"user@example.test\"}")]
    public async Task IdempotentEndpointMissingKeyShouldReturnBadRequestWithoutDispatching(string path, string body)
    {
        using var client = _app.GetTestClient();
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri(path, UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("IDENTITY_IDEMPOTENCY_KEY_REQUIRED", responseBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/v1/identity/auth/register", "{\"firstName\":\"Test\",\"lastName\":\"User\",\"email\":\"user@example.test\",\"password\":\"Password!123\",\"phone\":\"+12025550123\",\"tenantName\":\"Tenant\",\"tenantType\":0,\"taxId\":\"123\",\"taxCountry\":\"US\",\"fiscalCountry\":null,\"fiscalRegion\":null,\"fiscalProvince\":null,\"fiscalMunicipality\":null,\"fiscalPostalCode\":null,\"fiscalStreet\":null,\"planId\":\"starter\",\"channel\":\"web\"}")]
    [InlineData("/api/v1/identity/auth/login", "{\"email\":\"user@example.test\",\"password\":\"Password!123\",\"channel\":\"web\"}")]
    [InlineData("/api/v1/identity/auth/refresh", "{\"refreshToken\":\"token\"}")]
    [InlineData("/api/v1/identity/auth/invitations/accept", "{\"invitationToken\":\"token\",\"password\":\"Password!123\"}")]
    [InlineData("/api/v1/identity/users/invite", "{\"firstName\":\"Test\",\"lastName\":\"User\",\"email\":\"user@example.test\",\"phone\":null,\"role\":0}")]
    [InlineData("/api/v1/identity/auth/email/resend", "{\"email\":\"user@example.test\"}")]
    [InlineData("/api/v1/identity/auth/password/forgot", "{\"email\":\"user@example.test\"}")]
    public async Task IdempotentEndpointMalformedKeyShouldReturnBadRequest(string path, string body)
    {
        using var client = _app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", "invalid key with spaces");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("IDENTITY_IDEMPOTENCY_KEY_INVALID", responseBody, StringComparison.Ordinal);
    }
}
