using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Mcp.Application.Commands.RemoveMcpServer;

public sealed class RemoveMcpServerCommandHandler(
    ITenantManagementAuthorization authorization, ITenantContext tenantContext,
    IMcpProviderRepository repository) : IRequestHandler<RemoveMcpServerCommand, Result<Guid>>
{
    /// <summary>
    /// Soft deletes a server through its owning provider in the authenticated tenant.
    /// </summary>
    public async Task<Result<Guid>> Handle(RemoveMcpServerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = await McpManagementAccess.LoadAsync(request.ProviderId, authorization,
            tenantContext, repository, cancellationToken).ConfigureAwait(false);
        if (provider.IsFailure)
        {
            return ResultFactory.Failure<Guid>(provider.Error!);
        }

        var result = provider.Value!.RemoveServer(new McpServerId(request.ServerId), tenantContext.UserId);
        return result.IsSuccess
            ? ResultFactory.Success(request.ServerId)
            : ResultFactory.Failure<Guid>(result.Error!);
    }
}
