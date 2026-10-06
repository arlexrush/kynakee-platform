using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantSettings;

public sealed class UpdateTenantSettingsCommandHandler : IRequestHandler<UpdateTenantSettingsCommand, Result>
{
    private readonly IIdentityRepository _repository;
    private readonly ITenantContext _tenantContext;

    public UpdateTenantSettingsCommandHandler(IIdentityRepository repository, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Result> Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsAuthenticated || request.TenantId != _tenantContext.TenantId)
        {
            return ResultFactory.Failure(ApplicationError.Unauthorized(
                "IDENTITY_TENANT_SETTINGS_FORBIDDEN", "The current user cannot update this tenant."));
        }

        var membership = await _repository.GetTenantUserAsync(
                request.TenantId,
                _tenantContext.UserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (membership is null || membership.Role is not (UserRole.Owner or UserRole.Admin))
        {
            return ResultFactory.Failure(ApplicationError.Unauthorized(
                "IDENTITY_TENANT_SETTINGS_FORBIDDEN", "Only tenant owners and administrators can update tenant settings."));
        }

        var tenant = await _repository.GetTenantAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return ResultFactory.Failure(ApplicationError.NotFound(
                "IDENTITY_TENANT_NOT_FOUND", "The tenant was not found."));
        }

        var settings = CompanySettings.Create(
            request.Administration,
            request.Profit,
            request.Quality,
            request.SafetyHealth,
            request.Environment,
            request.Contingency);
        return settings.IsSuccess
            ? tenant.UpdateSettings(settings.Value!, _tenantContext.UserId)
            : ResultFactory.Failure(settings.Error!);
    }
}