using FluentAssertions;
using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.Mcp.Domain;

public sealed class McpQueryLogTests
{
    [Fact]
    public void QueryLogWithoutProjectShouldSucceed()
    {
        var result = CreateQueryLog(null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void QueryLogWithoutProjectShouldRetainNullProject()
    {
        var result = CreateQueryLog(null);

        result.Value!.ProjectId.Should().BeNull();
    }

    [Fact]
    public void QueryLogEmptyProjectShouldReturnValidation()
    {
        var result = CreateQueryLog(Guid.Empty);

        result.Error!.Code.Should().Be("MCP_QUERY_IDENTITY_REQUIRED");
    }

    [Fact]
    public void QueryLogValidProjectShouldRetainProject()
    {
        var projectId = Guid.NewGuid();
        var result = CreateQueryLog(projectId);

        result.Value!.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public void QueryLogValidAIPriceFallbackShouldRetainAuditData()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var providerId = McpProviderId.New();
        var queriedAt = DateTime.UtcNow;

        var result = McpQueryLog.Create(
            tenantId,
            projectId,
            providerId,
            "concept-1",
            McpComponentType.Material,
            2.5m,
            "kg",
            7.25m,
            "kg",
            McpFallbackSource.AI,
            0.82m,
            125,
            McpQueryStatus.Fallback,
            0m,
            queriedAt);

        result.Value.Should().BeEquivalentTo(new
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ProviderId = providerId,
            CanonicalConceptId = "concept-1",
            ComponentType = McpComponentType.Material,
            Quantity = 2.5m,
            Unit = "kg",
            ResponsePrice = (decimal?)7.25m,
            ResponseUnit = "kg",
            FallbackSource = McpFallbackSource.AI,
            FallbackActivated = true,
            Confidence = (decimal?)0.82m,
            DurationMilliseconds = (int?)125,
            Status = McpQueryStatus.Fallback,
            QueriedAt = queriedAt,
            IsDeleted = false
        });
    }

    [Fact]
    public void QueryLogFallbackWithoutPriceShouldReturnValidation()
    {
        var result = McpQueryLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            McpProviderId.New(),
            "concept-1",
            McpComponentType.Material,
            1m,
            "kg",
            null,
            null,
            McpFallbackSource.Cache,
            0.5m,
            10,
            McpQueryStatus.Fallback,
            0m,
            DateTime.UtcNow);

        result.Error.Should().BeEquivalentTo(new
        {
            Code = "MCP_QUERY_FALLBACK_INVALID",
            Type = ErrorType.Validation
        });
    }

    [Fact]
    public void QueryLogSuccessWithoutPriceShouldReturnValidation()
    {
        var result = McpQueryLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            McpProviderId.New(),
            "concept-1",
            McpComponentType.Material,
            1m,
            "kg",
            null,
            null,
            null,
            null,
            null,
            McpQueryStatus.Success,
            0m,
            DateTime.UtcNow);

        result.Error!.Code.Should().Be("MCP_QUERY_FALLBACK_INVALID");
    }

    private static Result<McpQueryLog> CreateQueryLog(Guid? projectId) =>
        McpQueryLog.Create(
            Guid.NewGuid(), projectId, McpProviderId.New(), "concept-1",
            McpComponentType.Material, 1m, "kg", 7.25m, "kg", null,
            0.82m, 125, McpQueryStatus.Success, 0m, DateTime.UtcNow);
}
