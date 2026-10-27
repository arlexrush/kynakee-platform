using System.Text.Json;
using FluentAssertions;
using Kynakee.Api.Endpoints;
using Kynakee.Modules.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kynakee.UnitTests.Api.Behaviors;

public sealed class ApiProblemsTests
{
    [Fact]
    public async Task ValidationErrorShouldReturnSafeProblemDetailsWithFieldErrors()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapGet("/", () => ApiProblems.FromError(ApplicationError.ValidationFields(["Password"]), 400));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        json.RootElement.GetProperty("title").GetString().Should().Be("VALIDATION_001");
        json.RootElement.GetProperty("errors").TryGetProperty("password", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ValidationErrorFieldAliasShouldUseHttpParameterName()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapGet("/", () => ApiProblems.FromError(ApplicationError.ValidationFields(["EndDate"]), 400,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["endDate"] = "to" }));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        json.RootElement.GetProperty("errors").TryGetProperty("to", out _).Should().BeTrue();
    }
}
