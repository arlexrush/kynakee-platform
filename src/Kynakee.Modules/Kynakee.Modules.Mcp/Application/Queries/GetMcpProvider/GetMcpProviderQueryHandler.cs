using System.Globalization;
using System.Resources;
using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Mcp.Application.Queries.GetMcpProvider;

public sealed class GetMcpProviderQueryHandler(
    ITenantManagementAuthorization authorization, McpDbContext context)
    : IRequestHandler<GetMcpProviderQuery, Result<McpProviderDetailDto>>
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Mcp.Application.McpManagementResources", typeof(GetMcpProviderQueryHandler).Assembly);

    /// <summary>
    /// Projects one tenant-owned provider and its live servers without exposing credential references.
    /// </summary>
    public async Task<Result<McpProviderDetailDto>> Handle(GetMcpProviderQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var permission = await authorization.AuthorizeAsync(cancellationToken).ConfigureAwait(false);
        if (permission.IsFailure)
        {
            return ResultFactory.Failure<McpProviderDetailDto>(permission.Error!);
        }
        if (request.ProviderId == Guid.Empty)
        {
            return ResultFactory.Failure<McpProviderDetailDto>(ApplicationError.Validation("MCP_PROVIDER_ID_INVALID",
                Resources.GetString("ProviderIdInvalid", CultureInfo.CurrentUICulture) ?? "ProviderIdInvalid"));
        }

        var providerId = new McpProviderId(request.ProviderId);
        var detail = await context.Providers.AsNoTracking().Where(provider => provider.Id == providerId)
            .Select(provider => new McpProviderDetailDto(provider.Id.Value, provider.Name, provider.Status,
                provider.Rating.Value, EF.Property<List<string>>(provider, "_categories"),
                EF.Property<List<string>>(provider, "_geoRegions"),
                provider.Servers.Where(server => !server.IsDeleted).OrderBy(server => server.Id)
                    .Select(server => new McpServerDto(server.Id.Value, server.Endpoint)).ToList()))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return detail is not null
            ? ResultFactory.Success(detail)
            : ResultFactory.Failure<McpProviderDetailDto>(ApplicationError.NotFound("MCP_PROVIDER_NOT_FOUND",
                Resources.GetString("ProviderNotFound", CultureInfo.CurrentUICulture) ?? "ProviderNotFound"));
    }
}
