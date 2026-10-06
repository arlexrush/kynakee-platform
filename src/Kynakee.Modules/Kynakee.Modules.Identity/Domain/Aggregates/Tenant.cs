using Kynakee.Modules.Identity.Domain.Events;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Domain.Aggregates;

public sealed class Tenant : AggregateRoot<Guid>
{
    private Tenant()
    {
    }

    private Tenant(
        Guid id,
        string name,
        TenantSlug slug,
        TenantType type,
        TaxId? taxId,
        Address? fiscalAddress,
        string planId,
        CompanyBranding branding,
        CompanySettings settings,
        Guid? createdBy)
        : base(id, id, createdBy)
    {
        Name = name;
        Slug = slug;
        Type = type;
        TaxId = taxId;
        FiscalAddress = fiscalAddress;
        PlanId = planId;
        Branding = branding;
        Settings = settings;
        Status = TenantStatus.Active;
    }

    public string Name { get; private set; } = string.Empty;

    public TenantSlug Slug { get; private set; } = default!;

    public TenantType Type { get; private set; }

    public TaxId? TaxId { get; private set; }

    public Address? FiscalAddress { get; private set; }

    public string PlanId { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }

    public CompanyBranding Branding { get; private set; } = CompanyBranding.Create().Value!;

    public CompanySettings Settings { get; private set; } = CompanySettings.Default;

    public static Result<Tenant> Create(
        string? name,
        TenantSlug? slug,
        TenantType type,
        TaxId? taxId,
        Address? fiscalAddress,
        string? planId,
        CompanyBranding? branding = null,
        CompanySettings? settings = null,
        Guid? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ResultFactory.Failure<Tenant>(
                ApplicationError.Validation("IDENTITY_TENANT_NAME_REQUIRED", "Tenant name is required."));
        }

        if (slug is null)
        {
            return ResultFactory.Failure<Tenant>(
                ApplicationError.Validation("IDENTITY_TENANT_SLUG_REQUIRED", "Tenant slug is required."));
        }

        if (!Enum.IsDefined(type))
        {
            return ResultFactory.Failure<Tenant>(
                ApplicationError.Validation("IDENTITY_TENANT_TYPE_INVALID", "Tenant type is invalid."));
        }

        if (string.IsNullOrWhiteSpace(planId))
        {
            return ResultFactory.Failure<Tenant>(
                ApplicationError.Validation("IDENTITY_PLAN_REQUIRED", "Tenant plan is required."));
        }

        if (taxId is not null && fiscalAddress is not null &&
            !string.Equals(taxId.CountryCode, fiscalAddress.Country, StringComparison.Ordinal))
        {
            return ResultFactory.Failure<Tenant>(
                ApplicationError.Validation("IDENTITY_TAX_COUNTRY_MISMATCH", "Tax identifier country must match the fiscal address country."));
        }

        var id = Guid.NewGuid();
        var tenant = new Tenant(
            id,
            name.Trim(),
            slug,
            type,
            taxId,
            fiscalAddress,
            planId.Trim(),
            branding ?? CompanyBranding.Create().Value!,
            settings ?? CompanySettings.Default,
            createdBy);

        tenant.AddDomainEvent(new TenantCreatedDomainEvent(id, tenant.Name));
        return ResultFactory.Success(tenant);
    }

    public Result UpdateSettings(CompanySettings settings, Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Status == TenantStatus.Cancelled)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_TENANT_CANCELLED", "A cancelled tenant cannot be changed."));
        }

        Settings = settings;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result UpdateProfile(
        string? name,
        TenantSlug? slug,
        TaxId? taxId,
        Address? fiscalAddress,
        string? planId,
        Guid? updatedBy = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_TENANT_NAME_REQUIRED", "Tenant name is required."));
        }

        if (slug is null || string.IsNullOrWhiteSpace(planId))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_TENANT_PROFILE_INVALID", "Tenant slug and plan are required."));
        }

        if (Status == TenantStatus.Cancelled)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_TENANT_CANCELLED", "A cancelled tenant cannot be changed."));
        }

        if (taxId is not null && fiscalAddress is not null &&
            !string.Equals(taxId.CountryCode, fiscalAddress.Country, StringComparison.Ordinal))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_TAX_COUNTRY_MISMATCH", "Tax identifier country must match the fiscal address country."));
        }

        Name = name.Trim();
        Slug = slug;
        TaxId = taxId;
        FiscalAddress = fiscalAddress;
        PlanId = planId.Trim();
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result UpdateBranding(CompanyBranding branding, Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(branding);
        if (Status == TenantStatus.Cancelled)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_TENANT_CANCELLED", "A cancelled tenant cannot be changed."));
        }

        Branding = branding;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result ChangeStatus(TenantStatus status, Guid? updatedBy = null)
    {
        if (!Enum.IsDefined(status))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_TENANT_STATUS_INVALID", "Tenant status is invalid."));
        }

        if (Status == TenantStatus.Cancelled)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_TENANT_CANCELLED", "A cancelled tenant cannot change status."));
        }

        if (Status == status)
        {
            return ResultFactory.Ok();
        }

        Status = status;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }
}