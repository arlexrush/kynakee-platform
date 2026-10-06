using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;

public sealed class UpdateTenantProfileCommandHandler : IRequestHandler<UpdateTenantProfileCommand, Result>
{
    private readonly IIdentityRepository _repository;
    private readonly ITenantContext _tenantContext;

    public UpdateTenantProfileCommandHandler(IIdentityRepository repository, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Result> Handle(UpdateTenantProfileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsAuthenticated || request.TenantId != _tenantContext.TenantId)
        {
            return Forbidden();
        }

        var membership = await _repository.GetTenantUserAsync(
                request.TenantId,
                _tenantContext.UserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (membership is null || membership.Role is not (UserRole.Owner or UserRole.Admin))
        {
            return Forbidden();
        }

        var tenant = await _repository.GetTenantAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return ResultFactory.Failure(ApplicationError.NotFound(
                "IDENTITY_TENANT_NOT_FOUND", "The tenant was not found."));
        }

        var slug = TenantSlug.Create(request.Slug);
        if (slug.IsSuccess && await _repository.TenantSlugExistsForOtherTenantAsync(
                slug.Value!.Value,
                request.TenantId,
                cancellationToken).ConfigureAwait(false))
        {
            return ResultFactory.Failure(ApplicationError.Conflict(
                "IDENTITY_TENANT_SLUG_ALREADY_USED", "The tenant slug is already in use."));
        }

        var taxId = request.TaxId is null ? null : TaxId.Create(request.TaxId, request.TaxCountry);
        var address = Address.Create(
            request.FiscalCountry,
            request.Region,
            request.Province,
            request.Municipality,
            request.PostalCode,
            request.Street);
        if (slug.IsFailure || taxId?.IsFailure == true || address.IsFailure)
        {
            return ResultFactory.Failure(slug.Error ?? taxId?.Error ?? address.Error!);
        }

        return tenant.UpdateProfile(
            request.Name,
            slug.Value,
            taxId?.Value,
            address.Value,
            request.PlanId,
            _tenantContext.UserId);
    }

    private static Result Forbidden() => ResultFactory.Failure(ApplicationError.Unauthorized(
        "IDENTITY_TENANT_PROFILE_FORBIDDEN", "Only tenant owners and administrators can update tenant profile."));
}