using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Representa un trabajo encolado para el envío de un correo de invitación que guarda de forma segura el contenido
/// cifrado y mantiene el estado de intentos, expiración y finalización.
/// </summary>
/// <remarks>Se crea mediante InvitationEmailJob.Create. El payload se persiste cifrado y se purga de memoria al
/// completar el trabajo. El método Retry aplica un retroceso exponencial acotado y completa el trabajo si se alcanzan
/// los intentos máximos o la fecha de expiración. Hereda de BaseEntity<Guid> y está asociado a un TenantId.</remarks>
public sealed class InvitationEmailJob : BaseEntity<Guid>
{
    private InvitationEmailJob() { }

    private InvitationEmailJob(Guid tenantId, Guid invitationId, Guid createdBy, byte[] payload, DateTimeOffset expiresAt, DateTimeOffset now)
        : base(Guid.NewGuid(), tenantId, createdBy)
    {
        InvitationId = invitationId;
        EncryptedPayload = payload.ToArray();
        ExpiresAt = expiresAt;
        NextAttemptAt = now;
    }

    public Guid InvitationId { get; private set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819", Justification = "EF persists this encrypted binary payload; no plaintext is exposed.")]
    public byte[] EncryptedPayload { get; private set; } = [];
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? FailureCode { get; private set; }

    /// <summary>Crea un trabajo con copia del contenido ya cifrado.</summary>
    public static InvitationEmailJob Create(Guid tenantId, Guid invitationId, Guid createdBy, byte[] payload, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(invitationId, Guid.Empty);
        return new(tenantId, invitationId, createdBy, payload, expiresAt, now);
    }

    /// <summary>Finaliza el trabajo y elimina el contenido sensible, incluido en cancelación/expiración.</summary>
    public void Complete(DateTimeOffset now, string? failureCode = null)
    {
        CompletedAt = now;
        FailureCode = failureCode;
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(EncryptedPayload);
        EncryptedPayload = [];
        RegisterUpdate();
    }

    /// <summary>Programa retry exponencial acotado sin guardar excepciones ni credenciales.</summary>
    public void Retry(DateTimeOffset now, string failureCode)
    {
        Attempts++;
        FailureCode = failureCode;
        NextAttemptAt = now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(Attempts - 1, 7))));
        if (Attempts >= 8 || NextAttemptAt >= ExpiresAt)
        {
            Complete(now, failureCode);
        }
        RegisterUpdate();
    }
}
