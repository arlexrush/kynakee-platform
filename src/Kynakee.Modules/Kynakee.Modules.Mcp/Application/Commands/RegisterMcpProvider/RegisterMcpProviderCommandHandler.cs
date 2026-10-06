using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Mcp.Application.Commands.RegisterMcpProvider;

public sealed class RegisterMcpProviderCommandHandler(
    ITenantManagementAuthorization authorization, ITenantContext tenantContext,
    IMcpProviderRepository repository) : IRequestHandler<RegisterMcpProviderCommand, Result<Guid>>
{
    /// <summary>
    /// Registers a provider owned by the authenticated tenant; the command pipeline commits persistence.
    /// </summary>
    public async Task<Result<Guid>> Handle(RegisterMcpProviderCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var permission = await authorization.AuthorizeAsync(cancellationToken).ConfigureAwait(false);
        if (permission.IsFailure)
        {
            return ResultFactory.Failure<Guid>(permission.Error!);
        }

        var provider = McpProvider.Create(tenantContext.TenantId, request.Name,
            request.Categories, request.GeoRegions, tenantContext.UserId);
        if (provider.IsFailure)
        {
            return ResultFactory.Failure<Guid>(provider.Error!);
        }

        await repository.AddAsync(provider.Value!, cancellationToken).ConfigureAwait(false);
        return ResultFactory.Success(provider.Value!.Id.Value);
    }
}
