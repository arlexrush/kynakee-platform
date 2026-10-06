using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Mcp.Domain.Entities;

public sealed class McpQueryLog : BaseEntity<McpQueryLogId>
{
    private McpQueryLog()
    {
    }

    private McpQueryLog(
        McpQueryLogId id,
        Guid tenantId,
        Guid? projectId,
        McpProviderId providerId,
        string canonicalConceptId,
        McpComponentType componentType,
        decimal quantity,
        string unit,
        decimal? responsePrice,
        string? responseUnit,
        McpFallbackSource? fallbackSource,
        decimal? confidence,
        int? durationMilliseconds,
        McpQueryStatus status,
        decimal creditsCharged,
        DateTime queriedAt,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        ProjectId = projectId;
        ProviderId = providerId;
        CanonicalConceptId = canonicalConceptId;
        ComponentType = componentType;
        Quantity = quantity;
        Unit = unit;
        ResponsePrice = responsePrice;
        ResponseUnit = responseUnit;
        FallbackSource = fallbackSource;
        Confidence = confidence;
        DurationMilliseconds = durationMilliseconds;
        Status = status;
        CreditsCharged = creditsCharged;
        QueriedAt = queriedAt;
    }

    public Guid? ProjectId { get; private set; }

    public McpProviderId ProviderId { get; private set; }

    public string CanonicalConceptId { get; private set; } = string.Empty;

    public McpComponentType ComponentType { get; private set; }

    public decimal Quantity { get; private set; }

    public string Unit { get; private set; } = string.Empty;

    public decimal? ResponsePrice { get; private set; }

    public string? ResponseUnit { get; private set; }

    public McpFallbackSource? FallbackSource { get; private set; }

    public bool FallbackActivated => FallbackSource.HasValue;

    public decimal? Confidence { get; private set; }

    public int? DurationMilliseconds { get; private set; }

    public McpQueryStatus Status { get; private set; }

    public decimal CreditsCharged { get; private set; }

    public DateTime QueriedAt { get; private set; }

    public static Result<McpQueryLog> Create(
        Guid tenantId,
        Guid? projectId,
        McpProviderId providerId,
        string? canonicalConceptId,
        McpComponentType componentType,
        decimal quantity,
        string? unit,
        decimal? responsePrice,
        string? responseUnit,
        McpFallbackSource? fallbackSource,
        decimal? confidence,
        int? durationMilliseconds,
        McpQueryStatus status,
        decimal creditsCharged,
        DateTime queriedAt,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || providerId.Value == Guid.Empty)
        {
            return Failure("MCP_QUERY_IDENTITY_REQUIRED", "Tenant and provider identifiers are required; the optional project identifier must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(canonicalConceptId) || canonicalConceptId.Trim().Length > 100)
        {
            return Failure("MCP_QUERY_CONCEPT_INVALID", "Canonical concept identifier is required and must not exceed 100 characters.");
        }

        if (!Enum.IsDefined(componentType) || !Enum.IsDefined(status) ||
            (fallbackSource.HasValue && !Enum.IsDefined(fallbackSource.Value)))
        {
            return Failure("MCP_QUERY_ENUM_INVALID", "Query component, status or fallback source is invalid.");
        }

        if (quantity <= 0 || string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 20)
        {
            return Failure("MCP_QUERY_ITEM_INVALID", "Query quantity and unit are invalid.");
        }

        if (responsePrice is < 0 || (responsePrice.HasValue && string.IsNullOrWhiteSpace(responseUnit)) ||
            (responseUnit?.Trim().Length > 20))
        {
            return Failure("MCP_QUERY_RESPONSE_INVALID", "Query response price or unit is invalid.");
        }

        if ((status == McpQueryStatus.Fallback) != fallbackSource.HasValue ||
            ((status is McpQueryStatus.Success or McpQueryStatus.Fallback) && !responsePrice.HasValue))
        {
            return Failure("MCP_QUERY_FALLBACK_INVALID", "Fallback status and response data must be consistent.");
        }

        if (confidence is < 0m or > 1m || durationMilliseconds is < 0 || creditsCharged < 0 ||
            queriedAt.Kind != DateTimeKind.Utc || createdBy == Guid.Empty)
        {
            return Failure("MCP_QUERY_AUDIT_INVALID", "Query audit values are invalid.");
        }

        return ResultFactory.Success(new McpQueryLog(
            McpQueryLogId.New(),
            tenantId,
            projectId,
            providerId,
            canonicalConceptId.Trim(),
            componentType,
            quantity,
            unit.Trim(),
            responsePrice,
            responseUnit?.Trim(),
            fallbackSource,
            confidence,
            durationMilliseconds,
            status,
            creditsCharged,
            queriedAt,
            createdBy));
    }

    private static Result<McpQueryLog> Failure(string code, string message) =>
        ResultFactory.Failure<McpQueryLog>(ApplicationError.Validation(code, message));
}
