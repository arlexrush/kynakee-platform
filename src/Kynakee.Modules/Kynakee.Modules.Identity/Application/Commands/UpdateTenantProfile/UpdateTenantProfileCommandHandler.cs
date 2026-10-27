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

        if (!await IdentityAccess.HasActiveMembershipAsync(
                _repository, request.TenantId, _tenantContext.UserId, cancellationToken, UserRole.Owner, UserRole.Admin)
                .ConfigureAwait(false))
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
        if (tenant.Version != request.ExpectedVersion)
        {
            return PreconditionFailed();
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

        var fields = request.SuppliedFiscalFields;
        var merged = request with
        {
            TaxId = fields.HasFlag(FiscalProfileFields.TaxId) ? request.TaxId : tenant.TaxId?.Value,
            TaxCountry = fields.HasFlag(FiscalProfileFields.TaxCountry) ? request.TaxCountry : tenant.TaxId?.CountryCode,
            FiscalCountry = fields.HasFlag(FiscalProfileFields.FiscalCountry) ? request.FiscalCountry : tenant.FiscalAddress?.Country,
            Region = fields.HasFlag(FiscalProfileFields.Region) ? request.Region : tenant.FiscalAddress?.Region,
            Province = fields.HasFlag(FiscalProfileFields.Province) ? request.Province : tenant.FiscalAddress?.Province,
            Municipality = fields.HasFlag(FiscalProfileFields.Municipality) ? request.Municipality : tenant.FiscalAddress?.Municipality,
            PostalCode = fields.HasFlag(FiscalProfileFields.PostalCode) ? request.PostalCode : tenant.FiscalAddress?.PostalCode,
            Street = fields.HasFlag(FiscalProfileFields.Street) ? request.Street : tenant.FiscalAddress?.Street,
            SuppliedFiscalFields = FiscalProfileFields.All
        };
        var validation = await new UpdateTenantProfileCommandValidator().ValidateAsync(merged, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return ResultFactory.Failure(ApplicationError.ValidationFields(validation.Errors.Select(failure => failure.PropertyName)));
        }

        var taxId = merged.TaxId is null ? null : TaxId.Create(merged.TaxId, merged.TaxCountry);
        Result<Address>? address = null;
        if (UpdateTenantProfileCommandValidator.HasFiscalAddress(merged))
        {
            address = Address.Create(
                merged.FiscalCountry,
                merged.Region,
                merged.Province,
                merged.Municipality,
                merged.PostalCode,
                merged.Street);
        }

        if (slug.IsFailure || taxId?.IsFailure == true || address?.IsFailure == true)
        {
            return ResultFactory.Failure(slug.Error ?? taxId?.Error ?? address?.Error!);
        }

        return tenant.UpdateProfile(
            request.Name,
            slug.Value,
            taxId?.Value,
            address?.Value,
            request.PlanId,
            _tenantContext.UserId);
    }

    private static Result Forbidden() => ResultFactory.Failure(ApplicationError.Unauthorized(
        "IDENTITY_TENANT_PROFILE_FORBIDDEN", "Only tenant owners and administrators can update tenant profile."));

    private static Result PreconditionFailed() => ResultFactory.Failure(ApplicationError.Conflict(
        "IDENTITY_PRECONDITION_FAILED", "The tenant profile has changed. Fetch the latest version before retrying."));
}