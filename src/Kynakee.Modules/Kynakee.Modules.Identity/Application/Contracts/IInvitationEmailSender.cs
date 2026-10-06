namespace Kynakee.Modules.Identity.Application.Contracts;

public interface IInvitationEmailSender
{
    Task SendInvitationAsync(
        string recipientEmail,
        string tenantName,
        string invitationToken,
        CancellationToken cancellationToken);
}