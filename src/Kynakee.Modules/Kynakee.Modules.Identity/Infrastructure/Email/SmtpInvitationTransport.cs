using System.Net;
using System.Net.Mail;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Modules.Identity.Infrastructure.Email;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the Identity module dependency injection container.")]
internal sealed class SmtpInvitationTransport : ISmtpInvitationTransport
{
    public async Task SendAsync(
        MailMessage message,
        string host,
        int port,
        string? username,
        string? password,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = checked((int)timeout.TotalMilliseconds)
        };
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        await client.SendMailAsync(message, timeoutSource.Token).ConfigureAwait(false);
    }
}
