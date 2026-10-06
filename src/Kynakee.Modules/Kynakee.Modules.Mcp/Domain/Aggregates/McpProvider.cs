using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Domain.Events;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Mcp.Domain.Aggregates;

public sealed class McpProvider : AggregateRoot<McpProviderId>
{
    private readonly List<string> _categories = [];
    private readonly List<string> _geoRegions = [];
    private readonly List<McpServer> _servers = [];

    private McpProvider()
    {
    }

    private McpProvider(
        McpProviderId id,
        Guid tenantId,
        string name,
        IReadOnlyCollection<string> categories,
        IReadOnlyCollection<string> geoRegions,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        Name = name;
        _categories.AddRange(categories);
        _geoRegions.AddRange(geoRegions);
        Status = McpProviderStatus.Active;
        Rating = ProviderRating.Default;
    }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<string> Categories => _categories.AsReadOnly();

    public IReadOnlyCollection<string> GeoRegions => _geoRegions.AsReadOnly();

    public IReadOnlyCollection<McpServer> Servers => _servers.AsReadOnly();

    public McpProviderStatus Status { get; private set; }

    public ProviderRating Rating { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public DateTime? LastSuccessAt { get; private set; }

    public DateTime? SuspendedUntil { get; private set; }

    public static Result<McpProvider> Create(
        Guid tenantId,
        string? name,
        IEnumerable<string>? categories,
        IEnumerable<string>? geoRegions,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure("MCP_TENANT_REQUIRED", "Provider tenant is required.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return Failure("MCP_PROVIDER_NAME_INVALID", "Provider name is required and must not exceed 200 characters.");
        }

        var normalizedCategories = NormalizeLabels(categories, 100);
        if (normalizedCategories is null)
        {
            return Failure("MCP_PROVIDER_CATEGORIES_INVALID", "At least one valid provider category is required.");
        }

        var normalizedRegions = NormalizeLabels(geoRegions, 50);
        if (normalizedRegions is null)
        {
            return Failure("MCP_PROVIDER_REGIONS_INVALID", "At least one valid provider region is required.");
        }

        if (createdBy == Guid.Empty)
        {
            return Failure("MCP_PROVIDER_CREATOR_INVALID", "Provider creator is invalid.");
        }

        return ResultFactory.Success(new McpProvider(
            McpProviderId.New(),
            tenantId,
            name.Trim(),
            normalizedCategories.Select(category => category.ToUpperInvariant()).ToArray(),
            normalizedRegions.Select(region => region.ToUpperInvariant()).ToArray(),
            createdBy));
    }

    public Result<McpServer> AddServer(
        Uri? endpoint,
        string? credentialSecretReference,
        Guid? createdBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure<McpServer>(ApplicationError.Conflict(
                "MCP_PROVIDER_DELETED",
                "A deleted provider cannot be changed."));
        }

        var server = McpServer.Create(
            Id,
            TenantId,
            endpoint,
            credentialSecretReference,
            createdBy ?? CreatedBy);
        if (server.IsFailure)
        {
            return server;
        }

        if (_servers.Any(existing => !existing.IsDeleted &&
            Uri.Compare(existing.Endpoint, server.Value!.Endpoint, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0))
        {
            return ResultFactory.Failure<McpServer>(ApplicationError.Conflict(
                "MCP_SERVER_ENDPOINT_DUPLICATE",
                "Provider already has this server endpoint."));
        }

        _servers.Add(server.Value!);
        RegisterUpdate(createdBy ?? CreatedBy);
        return server;
    }

    public Result RemoveServer(McpServerId serverId, Guid? deletedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(ApplicationError.Conflict(
                "MCP_PROVIDER_DELETED",
                "A deleted provider cannot be changed."));
        }

        var server = _servers.FirstOrDefault(candidate => candidate.Id == serverId && !candidate.IsDeleted);
        if (server is null)
        {
            return ResultFactory.Failure(ApplicationError.NotFound(
                "MCP_SERVER_NOT_FOUND",
                "Provider server was not found."));
        }

        if (deletedBy == Guid.Empty)
        {
            return ResultFactory.Failure(ApplicationError.Validation(
                "MCP_SERVER_DELETER_INVALID",
                "Server deleter is invalid."));
        }

        server.Delete(deletedBy);
        RegisterUpdate(deletedBy);
        return ResultFactory.Ok();
    }

    public Result RecordSuccess(DateTime? occurredAt = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(ApplicationError.Conflict(
                "MCP_PROVIDER_DELETED",
                "A deleted provider cannot record a query."));
        }

        var successAt = occurredAt ?? DateTime.UtcNow;
        ConsecutiveFailures = 0;
        LastSuccessAt = successAt;
        if (Status == McpProviderStatus.Suspended && SuspendedUntil <= successAt)
        {
            Status = McpProviderStatus.Active;
            SuspendedUntil = null;
        }

        RegisterUpdate();
        return ResultFactory.Ok();
    }

    public Result RecordFailure(DateTime? occurredAt = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(ApplicationError.Conflict(
                "MCP_PROVIDER_DELETED",
                "A deleted provider cannot record a query."));
        }

        ConsecutiveFailures++;
        if (ConsecutiveFailures >= 3 && Status != McpProviderStatus.Suspended)
        {
            Status = McpProviderStatus.Suspended;
            SuspendedUntil = (occurredAt ?? DateTime.UtcNow).AddSeconds(30);
            AddDomainEvent(new McpProviderFailedDomainEvent(Id, TenantId, ConsecutiveFailures));
        }

        RegisterUpdate();
        return ResultFactory.Ok();
    }

    public bool IsAvailableAt(DateTime utcNow) =>
        !IsDeleted && (Status == McpProviderStatus.Active ||
                       (Status == McpProviderStatus.Suspended &&
                        SuspendedUntil is not null && SuspendedUntil <= utcNow));

    public Result ChangeStatus(McpProviderStatus status, Guid? updatedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(ApplicationError.Conflict(
                "MCP_PROVIDER_DELETED",
                "A deleted provider cannot be changed."));
        }

        if (!Enum.IsDefined(status))
        {
            return ResultFactory.Failure(ApplicationError.Validation(
                "MCP_PROVIDER_STATUS_INVALID",
                "Provider status is invalid."));
        }

        if (updatedBy == Guid.Empty)
        {
            return ResultFactory.Failure(ApplicationError.Validation(
                "MCP_PROVIDER_UPDATER_INVALID",
                "Provider updater is invalid."));
        }

        Status = status;
        SuspendedUntil = null;
        if (status == McpProviderStatus.Active)
        {
            ConsecutiveFailures = 0;
        }

        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result UpdateRating(decimal rating, Guid? updatedBy = null)
    {
        var newRating = ProviderRating.Create(rating);
        if (newRating.IsFailure)
        {
            return ResultFactory.Failure(newRating.Error!);
        }

        if (IsDeleted)
        {
            return ResultFactory.Failure(ApplicationError.Conflict(
                "MCP_PROVIDER_DELETED",
                "A deleted provider cannot be changed."));
        }

        if (updatedBy == Guid.Empty)
        {
            return ResultFactory.Failure(ApplicationError.Validation(
                "MCP_PROVIDER_UPDATER_INVALID",
                "Provider updater is invalid."));
        }

        Rating = newRating.Value;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    private static string[]? NormalizeLabels(IEnumerable<string>? labels, int maxLength)
    {
        if (labels is null)
        {
            return null;
        }

        var normalized = labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .ToArray();
        if (normalized.Length == 0 || normalized.Any(label => label.Length > maxLength))
        {
            return null;
        }

        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static Result<McpProvider> Failure(string code, string message) =>
        ResultFactory.Failure<McpProvider>(ApplicationError.Validation(code, message));
}
