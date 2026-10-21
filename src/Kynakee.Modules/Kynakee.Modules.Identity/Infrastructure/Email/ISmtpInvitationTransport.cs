using System.Net.Mail;

namespace Kynakee.Modules.Identity.Infrastructure.Email;

internal interface ISmtpInvitationTransport
{
    Task SendAsync(
        MailMessage message,
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
