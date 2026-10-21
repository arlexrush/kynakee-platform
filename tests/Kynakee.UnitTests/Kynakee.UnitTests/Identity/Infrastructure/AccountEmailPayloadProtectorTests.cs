using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public sealed class AccountEmailPayloadProtectorTests
{
    [Fact]
    public void PayloadMatchingScopeShouldRoundTrip()
    {
        var protector = CreateProtector();
        var tenant = Guid.NewGuid();
        var action = Guid.NewGuid();
        var payload = new AccountEmailPayload("synthetic@example.test", "synthetic-token", AccountActionPurpose.VerifyEmail);
        var encrypted = protector.Protect(tenant, action, payload);

        Assert.Equal(payload, protector.Unprotect(tenant, action, encrypted));
    }

    [Fact]
    public void PayloadForeignTenantShouldReject()
    {
        var protector = CreateProtector();
        var action = Guid.NewGuid();
        var encrypted = protector.Protect(Guid.NewGuid(), action, Payload());

        Assert.Throws<AuthenticationTagMismatchException>(() => protector.Unprotect(Guid.NewGuid(), action, encrypted));
    }

    [Fact]
    public void PayloadForeignActionShouldReject()
    {
        var protector = CreateProtector();
        var tenant = Guid.NewGuid();
        var encrypted = protector.Protect(tenant, Guid.NewGuid(), Payload());

        Assert.Throws<AuthenticationTagMismatchException>(() => protector.Unprotect(tenant, Guid.NewGuid(), encrypted));
    }

    [Fact]
    public void PayloadTamperedShouldReject()
    {
        var protector = CreateProtector();
        var tenant = Guid.NewGuid();
        var action = Guid.NewGuid();
        var encrypted = protector.Protect(tenant, action, Payload());
        encrypted[^1] ^= 1;

        Assert.Throws<AuthenticationTagMismatchException>(() => protector.Unprotect(tenant, action, encrypted));
    }

    [Fact]
    public void PayloadInvitationCiphertextShouldReject()
    {
        var configuration = Configuration();
        var invitationProtector = new InvitationEmailPayloadProtector(configuration);
        var accountProtector = new AccountEmailPayloadProtector(configuration);
        var tenant = Guid.NewGuid();
        var action = Guid.NewGuid();
        var encrypted = invitationProtector.Protect(tenant, action, new InvitationEmailPayload("synthetic@example.test", "Synthetic", "token"));

        Assert.Throws<CryptographicException>(() => accountProtector.Unprotect(tenant, action, encrypted));
    }

    [Fact]
    public void EmailJobCompletedShouldErasePayload()
    {
        var now = DateTimeOffset.UtcNow;
        var job = AccountEmailJob.Create(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3], now.AddHours(1), now);

        job.Complete(now);

        Assert.Empty(job.EncryptedPayload);
    }

    [Fact]
    public void EmailJobFailedShouldScheduleRetry()
    {
        var now = DateTimeOffset.UtcNow;
        var job = AccountEmailJob.Create(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3], now.AddHours(1), now);

        job.Retry(now, "SMTP_FAILED");

        Assert.Equal(now.AddSeconds(30), job.NextAttemptAt);
    }

    private static AccountEmailPayload Payload() => new("synthetic@example.test", "synthetic-token", AccountActionPurpose.ResetPassword);
    private static AccountEmailPayloadProtector CreateProtector() => new(Configuration());
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Identity:InvitationEmail:ActiveKeyId"] = "v1",
        ["Identity:InvitationEmail:Keys:v1"] = Convert.ToBase64String(new byte[32])
    }).Build();
}
