using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Infrastructure.Persistence;

namespace Kynakee.Modules.Identity.Infrastructure.MailDelivery;

public sealed class InvitationEmailJobStore(IdentityDbContext dbContext, InvitationEmailPayloadProtector protector, TimeProvider timeProvider) : IInvitationEmailJobStore
{
    /// <summary>Encola el payload cifrado; SaveChanges y commit pertenecen a la transacción del comando.</summary>
    public void Enqueue(Guid tenantId, Guid invitationId, Guid createdBy, string recipientEmail,
        string tenantName, string invitationToken, DateTimeOffset expiresAt)
    {
        if (dbContext.Database.CurrentTransaction is null || tenantId != dbContext.CurrentTenantId)
        {
            throw new InvalidOperationException(IdentityReplayMessages.EmailTransactionRequired);
        }
        var encrypted = protector.Protect(tenantId, invitationId, new(recipientEmail, tenantName, invitationToken));
        dbContext.InvitationEmailJobs.Add(InvitationEmailJob.Create(tenantId, invitationId, createdBy, encrypted, expiresAt, timeProvider.GetUtcNow()));
    }
}
