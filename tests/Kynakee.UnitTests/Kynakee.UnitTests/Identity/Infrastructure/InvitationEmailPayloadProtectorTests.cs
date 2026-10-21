using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public sealed class InvitationEmailPayloadProtectorTests
{
    [Fact]
    public void PayloadRoundTripShouldPreserveInvitation()
    {
        var protector = CreateProtector();
        var tenant = Guid.NewGuid();
        var invitation = Guid.NewGuid();
        var payload = new InvitationEmailPayload("user@example.test", "Tenant", "secret-token");
        var encrypted = protector.Protect(tenant, invitation, payload);

        var restored = protector.Unprotect(tenant, invitation, encrypted);

        Assert.Equal(payload, restored);
    }

    [Fact]
    public void PayloadEncryptedStorageShouldNotContainInvitationToken()
    {
        var protector = CreateProtector();
        var encrypted = protector.Protect(Guid.NewGuid(), Guid.NewGuid(), new("user@example.test", "Tenant", "secret-token"));

        Assert.DoesNotContain("secret-token", System.Text.Encoding.UTF8.GetString(encrypted), StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadAnotherTenantShouldRejectDecryption()
    {
        var protector = CreateProtector();
        var invitation = Guid.NewGuid();
        var encrypted = protector.Protect(Guid.NewGuid(), invitation, new("user@example.test", "Tenant", "secret-token"));

        Assert.Throws<AuthenticationTagMismatchException>(() => protector.Unprotect(Guid.NewGuid(), invitation, encrypted));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ShortPayloadShouldRejectDecryption(int size)
    {
        var protector = CreateProtector();

        Assert.Throws<CryptographicException>(() => protector.Unprotect(Guid.NewGuid(), Guid.NewGuid(), new byte[size]));
    }

    private static InvitationEmailPayloadProtector CreateProtector() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:InvitationEmail:ActiveKeyId"] = "v1",
            ["Identity:InvitationEmail:Keys:v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build());
}
