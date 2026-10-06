using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Domain.ValueObjects;

namespace Kynakee.Modules.Mcp.Domain.Repositories;

public interface IMcpProviderRepository
{
    Task AddAsync(McpProvider provider, CancellationToken cancellationToken);

    Task<McpProvider?> GetByIdAsync(McpProviderId providerId, CancellationToken cancellationToken);

    Task<McpProvider?> GetByIdForUpdateAsync(McpProviderId providerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<McpProvider>> ListOwnedAsync(
        string? geoRegion,
        string? category,
        McpProviderStatus? status,
        int offset,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<McpProvider>> GetAvailableForQueryAsync(
        string geoRegion,
        string category,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
