using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Net.Mail;
using System.Collections.Concurrent;
using System.Text;

namespace Kynakee.Modules.Identity.Infrastructure.Email;

internal sealed class SmtpAccountEmailSender(IConfiguration configuration, ISmtpInvitationTransport transport, IHostEnvironment environment) : IAccountEmailSender
{
    private static readonly ConcurrentDictionary<(string Culture, string Resource), CompositeFormat> BodyFormats = new();

    public async Task SendAsync(string recipient, string token, AccountActionPurpose purpose, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        var host = configuration["Smtp:Host"];
        var from = configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException(AccountEmailMessages.Get("ConfigurationInvalid"));
        var port = configuration.GetValue("Smtp:Port", 587);
        if (port != 587) throw new InvalidOperationException(AccountEmailMessages.Get("ConfigurationInvalid"));
        var isVerification = purpose == AccountActionPurpose.VerifyEmail;
        var address = configuration[isVerification ? "Authentication:EmailVerificationBaseUrl" : "Authentication:PasswordResetBaseUrl"];
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             !(environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp &&
               string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException(AccountEmailMessages.Get(isVerification ? "VerificationUrlInvalid" : "PasswordResetUrlInvalid"));
        var value = new UriBuilder(uri) { Query = $"token={Uri.EscapeDataString(token)}" }.Uri.AbsoluteUri;
        var bodyResource = isVerification ? "VerifyBody" : "ResetBody";
        using var message = new MailMessage(new MailAddress(from), new MailAddress(recipient))
        {
            Subject = AccountEmailMessages.Get(isVerification ? "VerifySubject" : "ResetSubject"),
            Body = string.Format(CultureInfo.CurrentCulture,
                BodyFormats.GetOrAdd((CultureInfo.CurrentUICulture.Name, bodyResource), static key => CompositeFormat.Parse(AccountEmailMessages.Get(key.Resource))), value, expiresAt),
            IsBodyHtml = false
        };
        await transport.SendAsync(message, host, port, TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
    }
}
