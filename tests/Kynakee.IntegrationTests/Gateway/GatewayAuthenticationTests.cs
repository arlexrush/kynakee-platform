using System.Net;
using FluentAssertions;
using Kynakee.Gateway;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kynakee.IntegrationTests.Gateway;

public sealed class GatewayAuthenticationTests
{
    [Fact]
    public async Task GatewayRequestWithoutJwtShouldReturnUnauthorized()
    {
        using var factory = new TestGatewayFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed class TestGatewayFactory : WebApplicationFactory<GatewayEntryPointMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:SigningKey"] = new string('a', 32),
                    ["Authentication:Issuer"] = "Kynakee",
                    ["Authentication:Audience"] = "KynakeeClients"
                }));
    }
}