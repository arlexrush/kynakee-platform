using System.Globalization;
using System.Resources;

namespace Kynakee.Modules.Identity.Application.Contracts;

public static class IdentityReplayMessages
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Identity.Infrastructure.IdentityPersistenceResources", typeof(IdentityReplayMessages).Assembly);

    /// <summary>Mensaje localizable para un resultado almacenado no recuperable.</summary>
    public static string OutcomeUnavailable => Get("ReplayOutcomeUnavailable");
    /// <summary>Mensaje localizable para una sesión que ya no es válida.</summary>
    public static string SessionUnavailable => Get("ReplaySessionUnavailable");
    /// <summary>Mensaje localizable para permisos de invitación perdidos.</summary>
    public static string InvitationForbidden => Get("ReplayInvitationForbidden");
    /// <summary>Mensaje localizable para una invitación que ya no es utilizable.</summary>
    public static string InvitationUnavailable => Get("ReplayInvitationUnavailable");
    /// <summary>Mensaje localizable para configuración incompleta de claves independientes.</summary>
    public static string KeyConfigurationInvalid => Get("IdempotencyKeyConfigurationInvalid");
    /// <summary>Mensaje localizable para claves de correo pendientes de configuración.</summary>
    public static string EmailKeyConfigurationInvalid => Get("EmailKeyConfigurationInvalid");
    /// <summary>Mensaje localizable para contenido cifrado de correo no recuperable.</summary>
    public static string EmailPayloadUnavailable => Get("EmailPayloadUnavailable");
    /// <summary>Mensaje localizable para enqueue fuera del scope de comando.</summary>
    public static string EmailTransactionRequired => Get("EmailTransactionRequired");

    private static string Get(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException(name);
}
