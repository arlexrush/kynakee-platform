using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Globalization;
using System.Resources;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using MimeKit;

namespace Kynakee.Modules.Identity.Infrastructure.Email;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the Identity module dependency injection container.")]
internal sealed class SmtpInvitationTransport(IConfiguration configuration, Lazy<MailOAuthSession> session) : ISmtpInvitationTransport
{
    private static readonly ResourceManager Resources = new("Kynakee.Modules.Identity.Infrastructure.IdentityPersistenceResources", typeof(SmtpInvitationTransport).Assembly);

    public async Task SendAsync(
        MailMessage message,
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var username = configuration["Smtp:OAuth:Username"];
            if (string.IsNullOrWhiteSpace(username) ||
                !string.Equals(message.From?.Address, username, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(Resources.GetString("MailOAuthAccountMismatch", CultureInfo.CurrentUICulture));

            var token = await session.Value.GetAccessTokenAsync(timeoutSource.Token).ConfigureAwait(false);
            using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = checked((int)timeout.TotalMilliseconds) };
            using var mimeMessage = MimeMessage.CreateFromMailMessage(message);
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, timeoutSource.Token).ConfigureAwait(false);
            await client.AuthenticateAsync(new SaslMechanismOAuth2(username, token), timeoutSource.Token).ConfigureAwait(false);
            await client.SendAsync(mimeMessage, timeoutSource.Token).ConfigureAwait(false);
            // Dispose cierra la conexión: un fallo de QUIT después de aceptación no debe provocar otro envío.
        }
        catch (MailKit.CommandException) { throw TransportFailure(); }
        catch (MailKit.ProtocolException) { throw TransportFailure(); }
        catch (MailKit.Security.AuthenticationException) { throw TransportFailure(); }
        catch (System.Security.Authentication.AuthenticationException) { throw TransportFailure(); }
        catch (IOException) { throw TransportFailure(); }
        catch (System.Net.Sockets.SocketException) { throw TransportFailure(); }
        catch (MsalException) { throw TransportFailure(); }
        catch (NotSupportedException) { throw TransportFailure(); }
        catch (UnauthorizedAccessException) { throw TransportFailure(); }
    }

    private static SmtpException TransportFailure() => new(Resources.GetString("MailOAuthTransportFailed", CultureInfo.CurrentUICulture));
}
