using Kynakee.Modules.Identity.Application.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Net.Mail;
using System.Globalization;
using System.Resources;
using System.Text.Encodings.Web;

namespace Kynakee.Modules.Identity.Infrastructure.Email;

internal sealed class SmtpInvitationEmailSender : IInvitationEmailSender
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);
    private static readonly ResourceManager Resources = new("Kynakee.Modules.Identity.Infrastructure.IdentityPersistenceResources", typeof(SmtpInvitationEmailSender).Assembly);
    private readonly IConfiguration _configuration;
    private readonly ISmtpInvitationTransport _transport;
    private readonly IHostEnvironment _environment;

    public SmtpInvitationEmailSender(
        IConfiguration configuration,
        ISmtpInvitationTransport transport,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(environment);
        _configuration = configuration;
        _transport = transport;
        _environment = environment;
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
            (invitationUri.Scheme != Uri.UriSchemeHttps &&
             !(_environment.IsDevelopment() && invitationUri.Scheme == Uri.UriSchemeHttp &&
               string.Equals(invitationUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException(Resources.GetString("MailInvitationUrlInvalid", CultureInfo.CurrentUICulture));
        }

        var port = _configuration.GetValue("Smtp:Port", 587);
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException(Resources.GetString("MailPortInvalid", CultureInfo.CurrentUICulture));
        }

        using var message = new MailMessage(new MailAddress(from), new MailAddress(recipientEmail))
        {
            Subject = Resources.GetString("MailInvitationSubject", CultureInfo.CurrentUICulture),
            Body = CreateBody(invitationUri, tenantName, invitationToken),
            IsBodyHtml = true
        };

        await _transport.SendAsync(
                    message,
                    host,
                    port,
                    SendTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    private string RequiredSetting(string key) =>
        !string.IsNullOrWhiteSpace(_configuration[key]) ? _configuration[key]!
            : throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                Resources.GetString("MailSettingRequired", CultureInfo.CurrentUICulture)!, key));

    private static string CreateBody(Uri invitationUri, string tenantName, string invitationToken)
    {
        var invitationLink = new UriBuilder(invitationUri)
        {
            Query = $"token={Uri.EscapeDataString(invitationToken)}"
        }.Uri.AbsoluteUri;
        var safeTenantName = HtmlEncoder.Default.Encode(tenantName);
        var safeLink = HtmlEncoder.Default.Encode(invitationLink);
        return string.Format(CultureInfo.CurrentCulture, Resources.GetString("MailInvitationBody", CultureInfo.CurrentUICulture)!, safeTenantName, safeLink);
    }
}
