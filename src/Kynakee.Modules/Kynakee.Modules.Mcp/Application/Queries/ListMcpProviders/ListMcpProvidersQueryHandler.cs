using System.Globalization;
using System.Resources;
using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Mcp.Application.Queries.ListMcpProviders;

public sealed class ListMcpProvidersQueryHandler(
    ITenantManagementAuthorization authorization, McpDbContext context)
    : IRequestHandler<ListMcpProvidersQuery, Result<McpProviderPageDto>>
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Mcp.Application.McpManagementResources", typeof(ListMcpProvidersQueryHandler).Assembly);

    /// <summary>
    /// Projects a bounded page of providers owned by the authorized tenant without loading aggregates.
    /// </summary>
    public async Task<Result<McpProviderPageDto>> Handle(ListMcpProvidersQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var permission = await authorization.AuthorizeAsync(cancellationToken).ConfigureAwait(false);
        if (permission.IsFailure)
        {
            return ResultFactory.Failure<McpProviderPageDto>(permission.Error!);
        }

        if (request.Offset < 0 || request.PageSize is < 1 or > 100 ||
            request.Status.HasValue && !Enum.IsDefined(request.Status.Value) ||
            request.GeoRegion?.Length > 50 || request.Category?.Length > 100)
        {
            return ResultFactory.Failure<McpProviderPageDto>(ApplicationError.Validation("MCP_PROVIDER_FILTER_INVALID",
                Resources.GetString("FilterInvalid", CultureInfo.CurrentUICulture) ?? "FilterInvalid"));
        }

        var providers = context.Providers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.GeoRegion))
        {
            var region = request.GeoRegion.Trim().ToUpperInvariant();
            providers = providers.Where(provider => EF.Property<List<string>>(provider, "_geoRegions").Contains(region));
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim().ToUpperInvariant();
            providers = providers.Where(provider => EF.Property<List<string>>(provider, "_categories").Contains(category));
        }
        if (request.Status.HasValue)
        {
            providers = providers.Where(provider => provider.Status == request.Status.Value);
        }

        var items = await providers.OrderBy(provider => provider.Name).ThenBy(provider => provider.Id)
            .Skip(request.Offset).Take(request.PageSize + 1)
            .Select(provider => new McpProviderListItemDto(provider.Id.Value, provider.Name, provider.Status,
                provider.Rating.Value, provider.Servers.Count(server => !server.IsDeleted)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return ResultFactory.Success(new McpProviderPageDto(items.Take(request.PageSize).ToArray(),
            request.Offset, request.PageSize, items.Count > request.PageSize));
    }
}
