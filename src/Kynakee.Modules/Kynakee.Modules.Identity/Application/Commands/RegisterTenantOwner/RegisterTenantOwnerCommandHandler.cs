using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;

internal sealed class RegisterTenantOwnerCommandHandler : IRequestHandler<RegisterTenantOwnerCommand, Result<AccountActionAcknowledgement>>
{
    private readonly IIdentityRepository _repository;
    private readonly UserManager<User> _userManager;
    private readonly IAccountActionRepository _actions;
    private readonly AccountActionIssuer _issuer;

    public RegisterTenantOwnerCommandHandler(
        IIdentityRepository repository,
        UserManager<User> userManager,
        IAccountActionRepository actions,
        AccountActionIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(issuer);
        _repository = repository;
        _userManager = userManager;
        _actions = actions;
        _issuer = issuer;
    }

    /// <summary>
    /// Registra el propietario pendiente: valida entradas, crea tenant y membresía y encola la verificación sin sesión.
    /// </summary>
    /// <remarks>Puede fallar por validaciones de los Value Objects, errores en la creación de entidades o por
    /// fallos del UserManager; en dichos casos devuelve Result.Failure con el Error correspondiente.</remarks>
    /// <param name="request">Comando que contiene los datos necesarios para crear el tenant y el usuario propietario (nombre, contacto,
    /// identificadores fiscales, credenciales y configuración).</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Result que contiene un acuse genérico sin tokens en caso de éxito, o un Error detallado en
    /// caso de fallo.</returns>
    public async Task<Result<AccountActionAcknowledgement>> Handle(
        RegisterTenantOwnerCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = Email.Create(request.Email);
        var phone = PhoneNumber.Create(request.Phone);
        var taxId = TaxId.Create(request.TaxId, request.TaxCountry);
        Address? fiscalAddress = null;
        if (HasFiscalAddress(request))
        {
            var fiscalAddressResult = Address.Create(
                request.FiscalCountry,
                request.FiscalRegion,
                request.FiscalProvince,
                request.FiscalMunicipality,
                request.FiscalPostalCode,
                request.FiscalStreet);
            if (fiscalAddressResult.IsFailure)
            {
                return ResultFactory.Failure<AccountActionAcknowledgement>(fiscalAddressResult.Error!);
            }

            fiscalAddress = fiscalAddressResult.Value;
        }

        var branding = CompanyBranding.Create(companyName: request.TenantName);
        var slug = await CreateAvailableTenantSlugAsync(request.TenantName, cancellationToken).ConfigureAwait(false); // Genera un slug único basado en el nombre del tenant, verificando que no exista ya en la base de datos.

        if (email.IsFailure || phone.IsFailure || taxId.IsFailure || branding.IsFailure || slug.IsFailure)
        {
            var error = email.Error ?? phone.Error ?? taxId.Error ?? branding.Error ?? slug.Error!;
            return ResultFactory.Failure<AccountActionAcknowledgement>(error);
        }

        await _actions.LockEmailAsync(email.Value!.Value, cancellationToken).ConfigureAwait(false);
        if (await _repository.FindUserByEmailAcrossTenantsAsync(email.Value.Value, cancellationToken).ConfigureAwait(false) is not null)
            return ResultFactory.Success(AccountActionAcknowledgement.Pending);

        var tenantResult = Tenant.Create(
            request.TenantName,
            slug.Value,
            request.TenantType,
            taxId.Value,
            fiscalAddress,
            request.PlanId,
            branding.Value,
            CompanySettings.Default);

        if (tenantResult.IsFailure)
        {
            return ResultFactory.Failure<AccountActionAcknowledgement>(tenantResult.Error!);
        }

        var tenant = tenantResult.Value!; // Crea la entidad Tenant con los datos proporcionados, incluyendo el slug único, tipo de tenant, identificador fiscal, dirección fiscal opcional, plan de suscripción, branding y configuración predeterminada.
        
        var userResult = User.Create(
            tenant.Id,
            request.FirstName,
            request.LastName,
            email.Value,
            phone.Value,
            null,
            UserStatus.Inactive); // Crea la entidad User con los datos proporcionados, asociada al tenant recién creado, con estado Inactive hasta que se active la cuenta.


        if (userResult.IsFailure)
        {
            return ResultFactory.Failure<AccountActionAcknowledgement>(userResult.Error!);
        }

        var user = userResult.Value!; // Obtiene la entidad User creada anteriormente.

        _repository.AddTenant(tenant); // Persiste el tenant en el repositorio de identidad (base de datos).

        var identityResult = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false); // Crea el usuario en el sistema de identidad (Identity) con la contraseña proporcionada.

        if (!identityResult.Succeeded)
        {
            return MapIdentityFailure(identityResult); // Mapea los errores de Identity a un Result de fallo con un error detallado.
        }

        var activation = user.PrepareRegistration();
        if (activation.IsFailure)
        {
            return ResultFactory.Failure<AccountActionAcknowledgement>(activation.Error!);
        }

        var membershipResult = TenantUser.Create(user, UserRole.Owner); // Crea la entidad TenantUser que representa la membresía del usuario como propietario del tenant.
        
        if (membershipResult.IsFailure)
        {
            return ResultFactory.Failure<AccountActionAcknowledgement>(membershipResult.Error!);
        }

        var membership = membershipResult.Value!;
        _repository.AddTenantUser(membership);

        var issued = await _issuer.IssueAsync(user, AccountActionPurpose.VerifyEmail, cancellationToken).ConfigureAwait(false);
        return issued.IsFailure
            ? ResultFactory.Failure<AccountActionAcknowledgement>(issued.Error!)
            : ResultFactory.Success(AccountActionAcknowledgement.Pending);
    }

    private async Task<Result<TenantSlug>> CreateAvailableTenantSlugAsync(
        string tenantName,
        CancellationToken cancellationToken)
    {
        var baseSlug = CreateSlugBase(tenantName);
        if (baseSlug.Length == 0)
        {
            baseSlug = "tenant";
        }

        var candidate = baseSlug;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var parsedSlug = TenantSlug.Create(candidate);
            if (parsedSlug.IsSuccess &&
                !await _repository.TenantSlugExistsAsync(parsedSlug.Value!.Value, cancellationToken)
                    .ConfigureAwait(false))
            {
                return parsedSlug;
            }

            candidate = $"{baseSlug[..Math.Min(baseSlug.Length, 30)]}-{Guid.NewGuid():N}";
        }

        return ResultFactory.Failure<TenantSlug>(
            ApplicationError.Conflict("IDENTITY_TENANT_SLUG_UNAVAILABLE", "A unique tenant slug could not be generated."));
    }

    private static string CreateSlugBase(string value)
    {
        var decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var slug = new System.Text.StringBuilder(decomposed.Length);
        var previousWasSeparator = true;

#pragma warning disable CA1308 // Tenant slugs are defined as lowercase URL identifiers.
        foreach (var character in decomposed)
        {
            if (char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                slug.Append('-');
                previousWasSeparator = true;
            }
        }
#pragma warning restore CA1308

        return slug.ToString().Trim('-');
    }

    private static bool HasFiscalAddress(RegisterTenantOwnerCommand request) =>
        !string.IsNullOrWhiteSpace(request.FiscalCountry) ||
        !string.IsNullOrWhiteSpace(request.FiscalRegion) ||
        !string.IsNullOrWhiteSpace(request.FiscalProvince) ||
        !string.IsNullOrWhiteSpace(request.FiscalMunicipality) ||
        !string.IsNullOrWhiteSpace(request.FiscalPostalCode) ||
        !string.IsNullOrWhiteSpace(request.FiscalStreet);

    private static Result<AccountActionAcknowledgement> MapIdentityFailure(IdentityResult identityResult)
    {
        if (identityResult.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return ResultFactory.Failure<AccountActionAcknowledgement>(
                ApplicationError.Validation("IDENTITY_USER_CREATION_FAILED", AccountActionMessages.Get("Unavailable")));
        }

        return ResultFactory.Failure<AccountActionAcknowledgement>(
            ApplicationError.Validation("IDENTITY_USER_CREATION_FAILED", "Identity rejected the user registration."));
    }
}