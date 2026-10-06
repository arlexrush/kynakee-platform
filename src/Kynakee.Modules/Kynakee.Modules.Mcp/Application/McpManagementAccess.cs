using System.Globalization;
using System.Resources;
using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;

namespace Kynakee.Modules.Mcp.Application;

internal static class McpManagementAccess
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Mcp.Application.McpManagementResources", typeof(McpManagementAccess).Assembly);

    internal static async Task<Result<McpProvider>> LoadAsync(
        Guid providerId, ITenantManagementAuthorization authorization, ITenantContext tenantContext,
        IMcpProviderRepository repository, CancellationToken cancellationToken)
    {
        var permission = await authorization.AuthorizeAsync(cancellationToken).ConfigureAwait(false);
        if (permission.IsFailure)
        {
            return ResultFactory.Failure<McpProvider>(permission.Error!);
        }

        var provider = await repository.GetByIdForUpdateAsync(new McpProviderId(providerId), cancellationToken)
            .ConfigureAwait(false);
        return provider is not null && !provider.IsDeleted && provider.TenantId == tenantContext.TenantId
            ? ResultFactory.Success(provider)
            : ResultFactory.Failure<McpProvider>(ApplicationError.NotFound("MCP_PROVIDER_NOT_FOUND",
                Resources.GetString("ProviderNotFound", CultureInfo.CurrentUICulture) ?? "ProviderNotFound"));
    }
}
