using System.Net.Mail;
using System.Text.Encodings.Web;
using Kynakee.Modules.Identity.Application.Contracts;
using Microsoft.Extensions.Configuration;

namespace Kynakee.Modules.Identity.Infrastructure.Email;

internal sealed class SmtpInvitationEmailSender : IInvitationEmailSender
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);
    private readonly IConfiguration _configuration;
    private readonly ISmtpInvitationTransport _transport;

    public SmtpInvitationEmailSender(
        IConfiguration configuration,
        ISmtpInvitationTransport transport)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(transport);
        _configuration = configuration;
        _transport = transport;
    }

    public async Task SendInvitationAsync(
        string recipientEmail,
        string tenantName,
        string invitationToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);
        ArgumentException.ThrowIfNullOrWhiteSpace(invitationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var host = RequiredSetting("Smtp:Host");
        var from = RequiredSetting("Smtp:From");
        var invitationBaseUrl = RequiredSetting("Authentication:InvitationBaseUrl");
        if (!Uri.TryCreate(invitationBaseUrl, UriKind.Absolute, out var invitationUri) ||
            invitationUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Authentication:InvitationBaseUrl must be an absolute HTTPS URL.");
        }

        var port = _configuration.GetValue("Smtp:Port", 587);
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Smtp:Password is required when Smtp:Username is configured.");
        }

        using var message = new MailMessage(new MailAddress(from), new MailAddress(recipientEmail))
        {
            Subject = "Invitación a Kynakee",
            Body = CreateBody(invitationUri, tenantName, invitationToken),
            IsBodyHtml = true
        };

        try
        {
            await _transport.SendAsync(
                    message,
                    host,
                    port,
                    username,
                    password,
                    SendTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            message.Dispose();
        }
    }

    private string RequiredSetting(string key) =>
        _configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Configuration setting '{key}' is required to send invitation emails.");

    private static string CreateBody(Uri invitationUri, string tenantName, string invitationToken)
    {
        var invitationLink = new UriBuilder(invitationUri)
        {
            Query = $"token={Uri.EscapeDataString(invitationToken)}"
        }.Uri.AbsoluteUri;
        var safeTenantName = HtmlEncoder.Default.Encode(tenantName);
        var safeLink = HtmlEncoder.Default.Encode(invitationLink);
        return $"<p>Has sido invitado a {safeTenantName} en Kynakee.</p><p><a href=\"{safeLink}\">Aceptar invitación</a></p>";
    }
}
