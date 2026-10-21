using Kynakee.Modules.Identity.Infrastructure.Email;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public sealed class SmtpInvitationTransportTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("another@outlook.com")]
    public async Task TransportInvalidMailboxShouldRejectBeforeOAuthOrConnectionAsync(string? username)
    {
        var configuration = CreateConfiguration(username);
        var session = new Lazy<MailOAuthSession>(() => new MailOAuthSession(configuration));
        var transport = new SmtpInvitationTransport(configuration, session);
        using var message = new MailMessage("sender@outlook.com", "recipient@example.test");

        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(message, "smtp.example.test", 587,
            TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TransportCancelledRequestShouldNotInitializeOAuthOrConnectAsync()
    {
        var configuration = CreateConfiguration("sender@outlook.com");
        var session = new Lazy<MailOAuthSession>(() => new MailOAuthSession(configuration));
        var transport = new SmtpInvitationTransport(configuration, session);
        using var message = new MailMessage("sender@outlook.com", "recipient@example.test");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.SendAsync(message, "smtp.example.test", 587,
            TimeSpan.FromSeconds(15), cancellation.Token));
    }

    private static IConfiguration CreateConfiguration(string? username) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:OAuth:Username"] = username }).Build();
}
