using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Mcp.Infrastructure.Repositories;

public sealed class EfMcpProviderRepository : IMcpProviderRepository
{
    private readonly McpDbContext _dbContext;

    public EfMcpProviderRepository(McpDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task AddAsync(McpProvider provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        await _dbContext.Providers.AddAsync(provider, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<McpProvider?> GetByIdAsync(
        McpProviderId providerId,
        CancellationToken cancellationToken) =>
        _dbContext.Providers
            .AsNoTracking()
            .SingleOrDefaultAsync(provider => provider.Id == providerId, cancellationToken);

    public Task<McpProvider?> GetByIdForUpdateAsync(
        McpProviderId providerId,
        CancellationToken cancellationToken) =>
        _dbContext.Providers
            .AsTracking()
            .Include(provider => provider.Servers)
            .SingleOrDefaultAsync(provider => provider.Id == providerId, cancellationToken);

    public async Task<IReadOnlyList<McpProvider>> ListOwnedAsync(
        string? geoRegion,
        string? category,
        McpProviderStatus? status,
        int offset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var providers = _dbContext.Providers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(geoRegion))
        {
            var normalizedRegion = geoRegion.Trim().ToUpperInvariant();
            providers = providers.Where(provider =>
                EF.Property<List<string>>(provider, "_geoRegions").Contains(normalizedRegion));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim().ToUpperInvariant();
            providers = providers.Where(provider =>
                EF.Property<List<string>>(provider, "_categories").Contains(normalizedCategory));
        }

        if (status.HasValue)
        {
            providers = providers.Where(provider => provider.Status == status.Value);
        }

        return await providers
            .OrderByDescending(provider => provider.UpdatedAt)
            .ThenBy(provider => provider.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpProvider>> GetAvailableForQueryAsync(
        string geoRegion,
        string category,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geoRegion);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Query time must be UTC.", nameof(utcNow));
        }

        var normalizedRegion = geoRegion.Trim().ToUpperInvariant();
        var normalizedCategory = category.Trim().ToUpperInvariant();
        var providers = await _dbContext.Providers
            .IgnoreQueryFilters()
            .Include(provider => provider.Servers.Where(server => !server.IsDeleted))
            .Where(provider =>
                !provider.IsDeleted &&
                EF.Property<List<string>>(provider, "_geoRegions").Contains(normalizedRegion) &&
                EF.Property<List<string>>(provider, "_categories").Contains(normalizedCategory) &&
                (provider.Status == McpProviderStatus.Active ||
                 (provider.Status == McpProviderStatus.Suspended && provider.SuspendedUntil <= utcNow)) &&
                provider.Servers.Any(server => !server.IsDeleted))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return providers
            .OrderByDescending(provider => provider.Rating.Value)
            .ThenBy(provider => provider.Id.Value)
            .ToArray();
    }
}
