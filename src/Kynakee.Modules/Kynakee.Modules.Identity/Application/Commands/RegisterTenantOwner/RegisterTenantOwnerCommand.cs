using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using System.Text.Json.Serialization;

namespace Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;

/// <summary>
/// Solicita el alta del propietario de un tenant, pendiente de verificar su correo.
/// </summary>
/// <remarks>El comando produce un acuse genérico sin sesión y está sujeto a validaciones de
/// seguridad y políticas de tenant.</remarks>
/// <param name="FirstName">Nombre de pila del propietario.</param>
/// <param name="LastName">Apellido del propietario.</param>
/// <param name="Email">Correo electrónico utilizado como identificador de acceso.</param>
/// <param name="Password">Contraseña para la cuenta del propietario.</param>
/// <param name="Phone">Número de teléfono de contacto.</param>
/// <param name="TenantName">Nombre del tenant u organización a crear.</param>
/// <param name="TenantType">Categoría del tenant (por ejemplo, Empresa o Individual).</param>
/// <param name="TaxId">Identificador fiscal del tenant.</param>
/// <param name="TaxCountry">País que emitió el identificador fiscal, como código ISO de dos letras.</param>
/// <param name="FiscalCountry">País opcional de la dirección fiscal, como código ISO de dos letras.</param>
/// <param name="FiscalRegion">Región de la dirección fiscal.</param>
/// <param name="FiscalProvince">Provincia de la dirección fiscal.</param>
/// <param name="FiscalMunicipality">Municipio de la dirección fiscal.</param>
/// <param name="FiscalPostalCode">Código postal de la dirección fiscal.</param>
/// <param name="FiscalStreet">Calle y número de la dirección fiscal.</param>
/// <param name="PlanId">Identificador del plan de suscripción elegido.</param>
/// <param name="Channel">Canal de registro (por ejemplo: web, WhatsApp, Telegram).</param>
public sealed record RegisterTenantOwnerCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string Phone,
    string TenantName,
    TenantType TenantType,
    string TaxId,
    string TaxCountry,
    string? FiscalCountry,
    string? FiscalRegion,
    string? FiscalProvince,
    string? FiscalMunicipality,
    string? FiscalPostalCode,
    string? FiscalStreet,
    string PlanId,
    string Channel) : ICommand<AccountActionAcknowledgement>, IHttpIdempotentCommand
{
    [JsonIgnore]
    public HttpIdempotencyContext? IdempotencyContext { get; init; }
}
