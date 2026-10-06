using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Mcp.Application.Commands.AddMcpServer;

public sealed class AddMcpServerCommandHandler(
    ITenantManagementAuthorization authorization, ITenantContext tenantContext,
    IMcpProviderRepository repository) : IRequestHandler<AddMcpServerCommand, Result<Guid>>
{
    /// <summary>
    /// Adds a server through its owning provider without storing the credential itself.
    /// </summary>
    public async Task<Result<Guid>> Handle(AddMcpServerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = await McpManagementAccess.LoadAsync(request.ProviderId, authorization,
            tenantContext, repository, cancellationToken).ConfigureAwait(false);
        if (provider.IsFailure)
        {
            return ResultFactory.Failure<Guid>(provider.Error!);
        }

        var server = provider.Value!.AddServer(request.Endpoint, request.CredentialSecretReference, tenantContext.UserId);
        return server.IsSuccess
            ? ResultFactory.Success(server.Value!.Id.Value)
            : ResultFactory.Failure<Guid>(server.Error!);
    }
}
