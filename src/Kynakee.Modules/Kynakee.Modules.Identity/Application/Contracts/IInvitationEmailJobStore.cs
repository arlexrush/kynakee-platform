namespace Kynakee.Modules.Identity.Application.Contracts;

public interface IInvitationEmailJobStore
{
    /// <summary>Registra el trabajo cifrado en la transacción actual sin efectuar envío remoto.</summary>
    void Enqueue(Guid tenantId, Guid invitationId, Guid createdBy, string recipientEmail,
        string tenantName, string invitationToken, DateTimeOffset expiresAt);
}
