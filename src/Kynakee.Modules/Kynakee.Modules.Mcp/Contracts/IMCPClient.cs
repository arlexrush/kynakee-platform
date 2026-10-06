using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.Mcp.Domain.Enums;

namespace Kynakee.Modules.Mcp.Contracts;

/// <summary>Queries the provider network without knowledge of project state.</summary>
public interface IMCPClient
{
    /// <summary>Returns the first valid unit-price quote matching the requested unit and currency.</summary>
    Task<Result<MCPQueryResult>> QueryPriceAsync(MCPPriceQuery query, CancellationToken cancellationToken);

    /// <summary>Lists available providers without exposing server configuration or secrets.</summary>
    Task<Result<IReadOnlyList<MCPProviderSummaryDto>>> GetAvailableProvidersAsync(
        string geoRegion, string category, CancellationToken cancellationToken);
}

public sealed record MCPPriceQuery(
    string CanonicalConceptId,
    McpComponentType ComponentType,
    string Category,
    string Unit,
    decimal Quantity,
    string GeoRegion,
    string PostalCode,
    string Currency,
    Guid? ProjectId = null);

public sealed record MCPQueryResult(
    decimal Price,
    string Unit,
    string Currency,
    Guid ProviderId,
    string ProviderName,
    Guid ServerId,
    decimal Confidence,
    DateTime QueriedAt);

public sealed record MCPProviderSummaryDto(
    Guid Id, string Name, IReadOnlyList<string> Categories, IReadOnlyList<string> GeoRegions, decimal Rating);
