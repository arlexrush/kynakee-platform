using System.Net.Mail;
using FluentAssertions;
using Kynakee.Modules.Identity.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public class SmtpInvitationEmailSenderTests
{
    [Fact]
    public async Task SendInvitationShouldBuildEscapedHttpsMessageAndUseConfiguredTransport()
    {
        var transport = new CapturingSmtpInvitationTransport();
        var sender = new SmtpInvitationEmailSender(CreateConfiguration("https://app.example.test/invitations"), transport);

        await sender.SendInvitationAsync(
            "invitee@example.test",
            "Builders <North & South>",
            "opaque token&value",
            CancellationToken.None);

        transport.Host.Should().Be("smtp.example.test");
        transport.Port.Should().Be(2525);
        transport.Message.Should().NotBeNull();
        transport.Message!.From!.Address.Should().Be("no-reply@example.test");
        transport.Message.To.Single().Address.Should().Be("invitee@example.test");
        transport.Message.IsBodyHtml.Should().BeTrue();
        transport.Message.Body.Should().Contain("Builders &lt;North &amp; South&gt;");
        transport.Message.Body.Should().Contain("https://app.example.test/invitations?token=opaque%20token%26value");
        transport.Username.Should().Be("smtp-user");
        transport.Password.Should().Be("smtp-password");
        transport.Timeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task SendInvitationShouldRejectNonHttpsBaseUrlBeforeTransport()
    {
        var transport = new CapturingSmtpInvitationTransport();
        var sender = new SmtpInvitationEmailSender(CreateConfiguration("http://app.example.test/invitations"), transport);

        var act = () => sender.SendInvitationAsync(
            "invitee@example.test",
            "Tenant",
            "token",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        transport.Message.Should().BeNull();
    }

    [Fact]
    public async Task SendInvitationShouldRequirePasswordWhenSmtpUsernameIsConfigured()
    {
        var transport = new CapturingSmtpInvitationTransport();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.example.test",
                ["Smtp:From"] = "no-reply@example.test",
                ["Smtp:Port"] = "587",
                ["Smtp:Username"] = "smtp-user",
                ["Authentication:InvitationBaseUrl"] = "https://app.example.test/invitations"
            })
            .Build();
        var sender = new SmtpInvitationEmailSender(configuration, transport);

        var act = () => sender.SendInvitationAsync(
            "invitee@example.test",
            "Tenant",
            "token",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        transport.Message.Should().BeNull();
    }

    private static IConfiguration CreateConfiguration(string invitationBaseUrl) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.example.test",
                ["Smtp:From"] = "no-reply@example.test",
                ["Smtp:Port"] = "2525",
                ["Smtp:Username"] = "smtp-user",
                ["Smtp:Password"] = "smtp-password",
                ["Authentication:InvitationBaseUrl"] = invitationBaseUrl
            })
            .Build();

    private sealed class CapturingSmtpInvitationTransport : ISmtpInvitationTransport
    {
        public MailMessage? Message { get; private set; }

        public string? Host { get; private set; }

        public int Port { get; private set; }

        public string? Username { get; private set; }

        public string? Password { get; private set; }

        public TimeSpan Timeout { get; private set; }

        public Task SendAsync(
            MailMessage message,
            string host,
            int port,
            string? username,
            string? password,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Message = new MailMessage(message.From!, message.To.Single())
            {
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = message.IsBodyHtml
            };
            Host = host;
            Port = port;
            Username = username;
            Password = password;
            Timeout = timeout;
            return Task.CompletedTask;
        }
    }
}
