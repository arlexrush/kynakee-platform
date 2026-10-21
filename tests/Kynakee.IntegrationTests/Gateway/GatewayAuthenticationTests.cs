using FluentAssertions;
using Kynakee.Gateway;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Kynakee.IntegrationTests.Gateway;

public sealed class GatewayAuthenticationTests : IAsyncLifetime
{
    private WebApplication _backend = null!;
    private string _backendAddress = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _backend = builder.Build();
        _backend.MapPost("/{**path}", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
            return Results.Json(new ForwardedRequest(
                context.Request.Path.Value ?? string.Empty,
                context.Request.Headers["Idempotency-Key"].ToString(),
                body));
        });
        await _backend.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        _backendAddress = _backend.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public async ValueTask DisposeAsync() => await _backend.DisposeAsync().ConfigureAwait(true);

    [Fact]
    public async Task GatewayRequestWithoutJwtShouldReturnUnauthorized()
    {
        using var factory = new TestGatewayFactory(_backendAddress);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/v1/identity/auth/register")]
    [InlineData("/api/v1/identity/auth/login")]
    [InlineData("/api/v1/identity/auth/refresh")]
    [InlineData("/api/v1/identity/auth/logout")]
    [InlineData("/api/v1/identity/auth/invitations/accept")]
    [InlineData("/api/v1/identity/auth/email/verify")]
    [InlineData("/api/v1/identity/auth/email/resend")]
    [InlineData("/api/v1/identity/auth/password/forgot")]
    [InlineData("/api/v1/identity/auth/password/reset")]
    public async Task GatewayAnonymousIdentityPostShouldForwardBodyAndIdempotencyKey(string path)
    {
        using var factory = new TestGatewayFactory(_backendAddress);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = new StringContent("{\"email\":\"user@example.test\"}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", "0123456789abcdefghijkl");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var forwarded = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<ForwardedRequest>(
            response.Content, TestContext.Current.CancellationToken);

        forwarded.Should().Be(new ForwardedRequest(path, "0123456789abcdefghijkl", "{\"email\":\"user@example.test\"}"));
    }

    [Theory]
    [InlineData("GET", "/api/v1/identity/auth/register")]
    [InlineData("GET", "/api/v1/identity/auth/email/verify")]
    [InlineData("GET", "/api/v1/identity/auth/password/reset")]
    [InlineData("POST", "/api/v1/identity/users/invite")]
    [InlineData("POST", "/api/v1/identity/auth/unknown")]
    [InlineData("GET", "/api/v1/identity/tenants/me")]
    public async Task GatewayProtectedRouteWithoutJwtShouldReturnUnauthorized(string method, string path)
    {
        using var factory = new TestGatewayFactory(_backendAddress);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record ForwardedRequest(string Path, string Key, string Body);

    private sealed class TestGatewayFactory(string backendAddress) : WebApplicationFactory<GatewayEntryPointMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:SigningKey"] = new string('a', 32),
                    ["ReverseProxy:Clusters:api-cluster:Destinations:api:Address"] = backendAddress,
                    ["Authentication:Issuer"] = "Kynakee",
                    ["Authentication:Audience"] = "KynakeeClients"
                }));
    }
}