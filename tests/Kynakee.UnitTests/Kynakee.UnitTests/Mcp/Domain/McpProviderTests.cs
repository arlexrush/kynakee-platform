using FluentAssertions;
using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Domain.Events;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.Mcp.Domain;

public sealed class McpProviderTests
{
    [Fact]
    public void ProviderCreationValidTenantShouldInitializeOwnerAndCatalog()
    {
        var tenantId = Guid.NewGuid();
        var result = McpProvider.Create(
            tenantId,
            " Provider One ",
            [" Ceramics ", "ceramics"],
            ["es-vc", "ES-CT"]);

        result.Value.Should().BeEquivalentTo(new
        {
            TenantId = tenantId,
            Name = "Provider One",
            Status = McpProviderStatus.Active,
            Rating = ProviderRating.Default,
            IsDeleted = false
        });
        result.Value!.Categories.Should().Equal("CERAMICS");
        result.Value.GeoRegions.Should().Equal("ES-VC", "ES-CT");
    }

    [Fact]
    public void ProviderCreationMissingTenantShouldReturnValidation()
    {
        var result = McpProvider.Create(Guid.Empty, "Provider", ["ceramics"], ["ES-VC"]);

        result.Error.Should().BeEquivalentTo(new
        {
            Code = "MCP_TENANT_REQUIRED",
            Type = ErrorType.Validation
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProviderCreationMissingCategoryShouldReturnValidation(string? category)
    {
        var result = McpProvider.Create(Guid.NewGuid(), "Provider", [category!], ["ES-VC"]);

        result.Error!.Code.Should().Be("MCP_PROVIDER_CATEGORIES_INVALID");
    }

    [Fact]
    public void ProviderAddServerValidEndpointShouldRetainSecretReferenceAndTenant()
    {
        var provider = CreateProvider();

        var result = provider.AddServer(
            new Uri("https://ceramicas.example.test/mcp"),
            "vault://mcp/provider-one");

        result.Value.Should().BeEquivalentTo(new
        {
            TenantId = provider.TenantId,
            ProviderId = provider.Id,
            Endpoint = new Uri("https://ceramicas.example.test/mcp"),
            CredentialSecretReference = "vault://mcp/provider-one",
            IsDeleted = false
        });
    }

    [Theory]
    [InlineData("http://provider.example.test/mcp")]
    [InlineData("https://user:password@provider.example.test/mcp")]
    [InlineData("https://provider.example.test/mcp#secret")]
    public void ProviderAddServerUnsafeEndpointShouldReturnValidation(string endpoint)
    {
        var provider = CreateProvider();

        var result = provider.AddServer(new Uri(endpoint), "vault://mcp/provider-one");

        result.Error!.Code.Should().Be("MCP_SERVER_ENDPOINT_INVALID");
    }

    [Fact]
    public void ProviderAddServerDuplicateEndpointShouldReturnConflict()
    {
        var provider = CreateProvider();
        var endpoint = new Uri("https://ceramicas.example.test/mcp");
        provider.AddServer(endpoint, "vault://mcp/provider-one");

        var result = provider.AddServer(endpoint, "vault://mcp/provider-one");

        result.Error.Should().BeEquivalentTo(new
        {
            Code = "MCP_SERVER_ENDPOINT_DUPLICATE",
            Type = ErrorType.Conflict
        });
    }

    [Fact]
    public void ProviderRemoveServerValidIdShouldSoftDeleteServer()
    {
        var provider = CreateProvider();
        var server = provider.AddServer(
            new Uri("https://ceramicas.example.test/mcp"),
            "vault://mcp/provider-one").Value!;

        var result = provider.RemoveServer(server.Id, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        server.IsDeleted.Should().BeTrue();
        provider.Servers.Should().ContainSingle().Which.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void ProviderRecordFailureThirdConsecutiveShouldSuspendAndRaiseEvent()
    {
        var provider = CreateProvider();
        var occurredAt = DateTime.UtcNow;
        provider.RecordFailure(occurredAt);
        provider.RecordFailure(occurredAt.AddSeconds(1));

        provider.RecordFailure(occurredAt.AddSeconds(2));

        provider.Status.Should().Be(McpProviderStatus.Suspended);
        provider.SuspendedUntil.Should().Be(occurredAt.AddSeconds(32));
        provider.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<McpProviderFailedDomainEvent>();
    }

    [Fact]
    public void ProviderFailureCooldownShouldAllowQueryAfterThirtySeconds()
    {
        var provider = CreateProvider();
        var occurredAt = DateTime.UtcNow;
        provider.RecordFailure(occurredAt);
        provider.RecordFailure(occurredAt.AddSeconds(1));
        provider.RecordFailure(occurredAt.AddSeconds(2));

        provider.IsAvailableAt(occurredAt.AddSeconds(31)).Should().BeFalse();
        provider.IsAvailableAt(occurredAt.AddSeconds(32)).Should().BeTrue();
    }

    [Fact]
    public void ProviderRecordSuccessAfterCooldownShouldResetFailureState()
    {
        var provider = CreateProvider();
        var occurredAt = DateTime.UtcNow;
        provider.RecordFailure(occurredAt);
        provider.RecordFailure(occurredAt.AddSeconds(1));
        provider.RecordFailure(occurredAt.AddSeconds(2));

        provider.RecordSuccess(occurredAt.AddSeconds(32));

        provider.Status.Should().Be(McpProviderStatus.Active);
        provider.ConsecutiveFailures.Should().Be(0);
        provider.LastSuccessAt.Should().Be(occurredAt.AddSeconds(32));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(5.01)]
    public void ProviderUpdateRatingOutOfRangeShouldPreserveRating(decimal rating)
    {
        var provider = CreateProvider();

        var result = provider.UpdateRating(rating);

        result.Error!.Code.Should().Be("MCP_RATING_INVALID");
        provider.Rating.Should().Be(ProviderRating.Default);
    }

    private static McpProvider CreateProvider() =>
        McpProvider.Create(Guid.NewGuid(), "Provider", ["ceramics"], ["ES-VC"]).Value!;
}
