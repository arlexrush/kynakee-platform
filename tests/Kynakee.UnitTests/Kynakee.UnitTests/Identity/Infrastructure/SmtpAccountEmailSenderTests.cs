using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using System.Net.Mail;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public sealed class SmtpAccountEmailSenderTests
{
    [Theory]
    [InlineData("Development", "http://localhost:3000/auth/email/verify")]
    [InlineData("Production", "https://web.example.test/auth/email/verify")]
    public async Task VerifyEmailValidAddressShouldSendEscapedSingleUseLink(string environmentName, string address)
    {
        var transport = new CapturingTransport();
        var sender = CreateSender(environmentName, address, transport);

        await sender.SendAsync("owner@example.test", "opaque token&value", AccountActionPurpose.VerifyEmail,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        transport.Body.Should().Contain(address + "?token=opaque%20token%26value");
    }

    [Theory]
    [InlineData("Production", "http://localhost:3000/auth/email/verify")]
    [InlineData("Staging", "http://localhost:3000/auth/email/verify")]
    [InlineData("Development", "http://example.test/auth/email/verify")]
    [InlineData("Development", "http://127.0.0.1:3000/auth/email/verify")]
    [InlineData("Development", "http://localhost@evil.example.test/auth/email/verify")]
    [InlineData("Development", "ftp://localhost/auth/email/verify")]
    [InlineData("Production", "https://web.example.test/auth/email/verify?token=old")]
    [InlineData("Production", "https://web.example.test/auth/email/verify#section")]
    [InlineData("Production", "https://user@web.example.test/auth/email/verify")]
    [InlineData("Production", "invalid")]
    [InlineData("Production", null)]
    public async Task VerifyEmailInvalidAddressShouldRejectBeforeTransport(string environmentName, string? address)
    {
        var transport = new CapturingTransport();
        var sender = CreateSender(environmentName, address, transport);

        var act = () => sender.SendAsync("owner@example.test", "token", AccountActionPurpose.VerifyEmail,
            DateTimeOffset.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        transport.Body.Should().BeNull();
    }

    [Theory]
    [InlineData("Development", "http://localhost:3000/auth/password/reset")]
    [InlineData("Production", "https://web.example.test/auth/password/reset")]
    public async Task ResetPasswordValidAddressShouldSendEscapedSingleUseLink(string environmentName, string address)
    {
        var transport = new CapturingTransport();
        var sender = CreateSender(environmentName, null, transport, address);

        await sender.SendAsync("owner@example.test", "opaque token&value", AccountActionPurpose.ResetPassword,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        transport.Body.Should().Contain(address + "?token=opaque%20token%26value");
    }

    [Theory]
    [InlineData("Production", "http://localhost:3000/auth/password/reset")]
    [InlineData("Staging", "http://localhost:3000/auth/password/reset")]
    [InlineData("Development", "http://example.test/auth/password/reset")]
    [InlineData("Development", "http://127.0.0.1:3000/auth/password/reset")]
    [InlineData("Development", "http://localhost@evil.example.test/auth/password/reset")]
    [InlineData("Development", "ftp://localhost/auth/password/reset")]
    [InlineData("Production", "https://web.example.test/auth/password/reset?token=old")]
    [InlineData("Production", "https://web.example.test/auth/password/reset#section")]
    [InlineData("Production", "https://user@web.example.test/auth/password/reset")]
    [InlineData("Production", "invalid")]
    [InlineData("Production", null)]
    public async Task ResetPasswordInvalidAddressShouldRejectBeforeTransport(string environmentName, string? address)
    {
        var transport = new CapturingTransport();
        var sender = CreateSender(environmentName, "https://web.example.test/auth/email/verify", transport, address);

        var act = () => sender.SendAsync("owner@example.test", "token", AccountActionPurpose.ResetPassword,
            DateTimeOffset.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        transport.Body.Should().BeNull();
    }

    private static SmtpAccountEmailSender CreateSender(string environmentName, string? address, CapturingTransport transport, string? resetAddress = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.test",
            ["Smtp:From"] = "sender@example.test",
            ["Smtp:Port"] = "587",
            ["Authentication:EmailVerificationBaseUrl"] = address,
            ["Authentication:PasswordResetBaseUrl"] = resetAddress
        }).Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new SmtpAccountEmailSender(configuration, transport, environment);
    }

    private sealed class CapturingTransport : ISmtpInvitationTransport
    {
        public string? Body { get; private set; }

        public Task SendAsync(MailMessage message, string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Body = message.Body;
            return Task.CompletedTask;
        }
    }
}
