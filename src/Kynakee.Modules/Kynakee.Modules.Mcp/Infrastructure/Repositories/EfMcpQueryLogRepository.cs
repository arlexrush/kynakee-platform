using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Mcp.Infrastructure.Repositories;

public sealed class EfMcpQueryLogRepository : IMcpQueryLogRepository
{
    private readonly McpDbContext _dbContext;

    public EfMcpQueryLogRepository(McpDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task AddAsync(McpQueryLog queryLog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryLog);

        await _dbContext.QueryLogs.AddAsync(queryLog, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpQueryLog>> GetByProviderIdAsync(
        McpProviderId providerId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        return await _dbContext.QueryLogs
            .AsNoTracking()
            .Where(queryLog => queryLog.ProviderId == providerId)
            .OrderByDescending(queryLog => queryLog.QueriedAt)
            .ThenByDescending(queryLog => queryLog.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
