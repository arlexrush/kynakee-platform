using Kynakee.Modules.Identity.Domain.Events;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Domain.Aggregates;

/// <summary>
/// Agregado que representa una organización (tenant) con identidad, configuración, marca, plan y estado; encapsula la
/// validación y las operaciones de creación y modificación del tenant y publica eventos de dominio relevantes.
/// </summary>
/// <remarks>La instancia se crea mediante el método estático Create que devuelve Result<Tenant> y realiza
/// validaciones de negocio. Las mutaciones (perfil, branding, settings y estado) validan la coherencia (por ejemplo,
/// bloqueo cuando el tenant está cancelado) y registran actualizaciones mediante eventos de dominio.</remarks>
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

    /// <summary>
    /// Nombre del tenant.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Slug del tenant.
    /// </summary>
    public TenantSlug Slug { get; private set; } = default!;

    /// <summary>
    /// Tipo del tenant.
    /// </summary>
    public TenantType Type { get; private set; }

    /// <summary>
    /// Identificador fiscal del tenant.
    /// </summary>
    public TaxId? TaxId { get; private set; }

    /// <summary>
    /// Dirección fiscal del tenant.
    /// </summary>
    public Address? FiscalAddress { get; private set; }

    /// <summary>
    /// Identificador del plan del tenant.
    /// </summary>
    public string PlanId { get; private set; } = string.Empty;

    /// <summary>
    /// Estado del tenant.
    /// </summary>
    public TenantStatus Status { get; private set; }

    /// <summary>
    /// Información de marca de la empresa.
    /// </summary>
    public CompanyBranding Branding { get; private set; } = CompanyBranding.Create().Value!;

    /// <summary>
    /// Configuración de la empresa.
    /// </summary>
    public CompanySettings Settings { get; private set; } = CompanySettings.Default;

    /// <summary>
    /// Crea una nueva instancia de Tenant con los valores especificados.
    /// </summary>
    /// <param name="name">Nombre del tenant.</param>
    /// <param name="slug">Slug del tenant.</param>
    /// <param name="type">Tipo del tenant.</param>
    /// <param name="taxId">Identificador fiscal del tenant.</param>
    /// <param name="fiscalAddress">Dirección fiscal del tenant.</param>
    /// <param name="planId">Identificador del plan del tenant.</param>
    /// <param name="branding">Información de marca de la empresa.</param>
    /// <param name="settings">Configuración de la empresa.</param>
    /// <param name="createdBy">Identificador del usuario que crea el tenant.</param>
    /// <returns>Resultado que contiene la instancia de Tenant creada o un error de validación.</returns>
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

    /// <summary>
    /// Actualiza la configuración de la empresa del tenant cuando su estado no es Cancelled..
    /// </summary>
    /// <remarks>Lanza ArgumentNullException si settings es nulo. Asigna la propiedad Settings y registra la
    /// actualización mediante RegisterUpdate(updatedBy).</remarks>
    /// <param name="settings">Configuración de la empresa a aplicar. No puede ser nulo.</param>
    /// <param name="updatedBy">Identificador opcional del usuario que realiza la actualización; nulo si no se especifica.</param>
    /// <returns>Result que indica éxito o fallo; devuelve fallo si el tenant está en estado Cancelled y no se permiten cambios.</returns>
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