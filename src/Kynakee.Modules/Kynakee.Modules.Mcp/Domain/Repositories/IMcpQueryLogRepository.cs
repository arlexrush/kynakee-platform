using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.ValueObjects;

namespace Kynakee.Modules.Mcp.Domain.Repositories;

public interface IMcpQueryLogRepository
{
    Task AddAsync(McpQueryLog queryLog, CancellationToken cancellationToken);

    Task<IReadOnlyList<McpQueryLog>> GetByProviderIdAsync(
        McpProviderId providerId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken);
}
