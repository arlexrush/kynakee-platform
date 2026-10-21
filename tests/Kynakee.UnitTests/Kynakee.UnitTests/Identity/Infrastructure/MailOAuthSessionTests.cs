using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public sealed class MailOAuthSessionTests
{
    [Theory]
    [InlineData("ClientId", "invalid")]
    [InlineData("TenantId", "common")]
    [InlineData("Username", "")]
    [InlineData("CachePath", "relative.cache")]
    [InlineData("CacheKey", "invalid")]
    [InlineData("CacheKey", "AQID")]
    public void SessionInvalidConfigurationShouldRejectConstruction(string setting, string value)
    {
        var settings = CreateSettings();
        settings["Smtp:OAuth:" + setting] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Assert.Throws<InvalidOperationException>(() => new MailOAuthSession(configuration));
    }

    [Fact]
    public async Task SessionCancelledRequestShouldNotContactIdentityProviderAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(CreateSettings()).Build();
        var session = new MailOAuthSession(configuration);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.GetAccessTokenAsync(cancellation.Token));
    }

    private static Dictionary<string, string?> CreateSettings() => new()
    {
        ["Smtp:OAuth:ClientId"] = Guid.NewGuid().ToString("D"),
        ["Smtp:OAuth:TenantId"] = "consumers",
        ["Smtp:OAuth:Username"] = "sender@outlook.com",
        ["Smtp:OAuth:CachePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mail.cache"),
        ["Smtp:OAuth:CacheKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    };
}
