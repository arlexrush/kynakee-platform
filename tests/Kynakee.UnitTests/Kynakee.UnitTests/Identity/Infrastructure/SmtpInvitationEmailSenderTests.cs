using FluentAssertions;
using Kynakee.Modules.Identity.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using System.Net.Mail;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public class SmtpInvitationEmailSenderTests
{
    [Fact]
    public async Task SendInvitationShouldBuildEscapedHttpsMessageAndUseConfiguredTransport()
    {
        var transport = new CapturingSmtpInvitationTransport();
        var sender = new SmtpInvitationEmailSender(CreateConfiguration("https://app.example.test/invitations"), transport, CreateEnvironment(Environments.Production));

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
        transport.Timeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task SendInvitationShouldRejectNonHttpsBaseUrlBeforeTransport()
    {
        var transport = new CapturingSmtpInvitationTransport();
        var sender = new SmtpInvitationEmailSender(CreateConfiguration("http://app.example.test/invitations"), transport, CreateEnvironment(Environments.Production));

        var act = () => sender.SendInvitationAsync(
            "invitee@example.test",
            "Tenant",
            "token",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        transport.Message.Should().BeNull();
    }

    [Fact]
    public async Task SendInvitationLocalhostInDevelopmentShouldBuildHttpMessage()
    {
        var transport = new CapturingSmtpInvitationTransport();
        var sender = new SmtpInvitationEmailSender(CreateConfiguration("http://localhost:3000/invitations/accept"), transport, CreateEnvironment(Environments.Development));

        await sender.SendInvitationAsync("invitee@example.test", "Tenant", "opaque token&value", CancellationToken.None);

        transport.Message!.Body.Should().Contain("http://localhost:3000/invitations/accept?token=opaque%20token%26value");
    }

    [Theory]
    [InlineData("Production", "http://localhost:3000/invitations/accept")]
    [InlineData("Staging", "http://localhost:3000/invitations/accept")]
    [InlineData("Development", "http://app.example.test/invitations/accept")]
    [InlineData("Development", "http://localhost.example.test/invitations/accept")]
    [InlineData("Development", "http://localhost@evil.example.test/invitations/accept")]
    [InlineData("Development", "http://127.0.0.1:3000/invitations/accept")]
    [InlineData("Development", "ftp://localhost/invitations/accept")]
    public async Task SendInvitationInvalidEnvironmentOrHostShouldRejectBeforeTransport(string environment, string address)
    {
        var transport = new CapturingSmtpInvitationTransport();
        var sender = new SmtpInvitationEmailSender(CreateConfiguration(address), transport, CreateEnvironment(environment));

        var act = () => sender.SendInvitationAsync("invitee@example.test", "Tenant", "token", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        transport.Message.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task SendInvitationInvalidPortShouldRejectBeforeTransport(int port)
    {
        var transport = new CapturingSmtpInvitationTransport();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.example.test",
                ["Smtp:From"] = "no-reply@example.test",
                ["Smtp:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Authentication:InvitationBaseUrl"] = "https://app.example.test/invitations"
            })
            .Build();
        var sender = new SmtpInvitationEmailSender(configuration, transport, CreateEnvironment(Environments.Production));

        var act = () => sender.SendInvitationAsync(
            "invitee@example.test",
            "Tenant",
            "token",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        transport.Message.Should().BeNull();
    }

    private static IHostEnvironment CreateEnvironment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }

    private static IConfiguration CreateConfiguration(string invitationBaseUrl) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.example.test",
                ["Smtp:From"] = "no-reply@example.test",
                ["Smtp:Port"] = "2525",
                ["Authentication:InvitationBaseUrl"] = invitationBaseUrl
            })
            .Build();

    private sealed class CapturingSmtpInvitationTransport : ISmtpInvitationTransport
    {
        public MailMessage? Message { get; private set; }

        public string? Host { get; private set; }

        public int Port { get; private set; }

        public TimeSpan Timeout { get; private set; }

        public Task SendAsync(
            MailMessage message,
            string host,
            int port,
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
            Timeout = timeout;
            return Task.CompletedTask;
        }
    }
}
