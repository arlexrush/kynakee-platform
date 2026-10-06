using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Mcp.Application.Commands.ChangeMcpProviderStatus;

public sealed class ChangeMcpProviderStatusCommandHandler(
    ITenantManagementAuthorization authorization, ITenantContext tenantContext,
    IMcpProviderRepository repository) : IRequestHandler<ChangeMcpProviderStatusCommand, Result<Guid>>
{
    /// <summary>
    /// Changes provider availability through the aggregate after checking tenant management permissions.
    /// </summary>
    public async Task<Result<Guid>> Handle(ChangeMcpProviderStatusCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = await McpManagementAccess.LoadAsync(request.ProviderId, authorization,
            tenantContext, repository, cancellationToken).ConfigureAwait(false);
        if (provider.IsFailure)
        {
            return ResultFactory.Failure<Guid>(provider.Error!);
        }

        var result = provider.Value!.ChangeStatus(request.Status, tenantContext.UserId);
        return result.IsSuccess
            ? ResultFactory.Success(provider.Value.Id.Value)
            : ResultFactory.Failure<Guid>(result.Error!);
    }
}
