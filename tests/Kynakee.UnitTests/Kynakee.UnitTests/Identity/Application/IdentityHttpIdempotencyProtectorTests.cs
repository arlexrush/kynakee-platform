using Kynakee.Api.Infrastructure.Identity;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Kynakee.UnitTests.Identity.Application;

public sealed class IdentityHttpIdempotencyProtectorTests
{
    private readonly IdentityHttpIdempotencyProtector _protector = new(new string('k', 32));

    [Fact]
    public void LegacyHashRootImportedAsIndependentKeyShouldPreserveClaim()
    {
        var root = new string('k', 48);
        var context = new HttpIdempotencyContext("identity.login", "0123456789abcdefghijkl", Guid.Empty, Guid.Empty);
        var request = new { Email = "user@example.test" };
        var configured = new IdentityHttpIdempotencyProtector(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Identity:Idempotency:HashKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(root)),
                ["Identity:Idempotency:ActiveOutcomeKeyId"] = "new",
                ["Identity:Idempotency:OutcomeKeys:new"] = Convert.ToBase64String(new byte[32])
            }).Build());

        Assert.Equal(new IdentityHttpIdempotencyProtector(root).CreateRequest(request, context),
            configured.CreateRequest(request, context));
    }

    [Fact]
    public void LegacyEncryptedOutcomeExplicitKeyShouldRestorePreviousResult()
    {
        var root = RandomNumberGenerator.GetBytes(32);
        var legacyKey = RandomNumberGenerator.GetBytes(32);
        var plaintext = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes("completed");
        var outcome = new byte[28 + plaintext.Length];
        RandomNumberGenerator.Fill(outcome.AsSpan(0, 12));
        using var aes = new AesGcm(legacyKey, 16);
        aes.Encrypt(outcome.AsSpan(0, 12), plaintext, outcome.AsSpan(28), outcome.AsSpan(12, 16));
        var configured = new IdentityHttpIdempotencyProtector(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Identity:Idempotency:HashKey"] = Convert.ToBase64String(root),
                ["Identity:Idempotency:ActiveOutcomeKeyId"] = "new",
                ["Identity:Idempotency:OutcomeKeys:new"] = Convert.ToBase64String(new byte[32]),
                ["Identity:Idempotency:AllowLegacyOutcomes"] = "true",
                ["Identity:Idempotency:LegacyOutcomeKey"] = Convert.ToBase64String(legacyKey)
            }).Build());

        var result = (Result<string>)configured.RestoreSuccess(outcome, typeof(Result<string>));

        Assert.Equal("completed", result.Value);
    }

    [Fact]
    public void OutcomeUnknownKeyShouldFailClosed()
    {
        var outcome = ConfiguredProtector("old").ProtectSuccess(ResultFactory.Success("completed"));
        var configured = new IdentityHttpIdempotencyProtector(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Identity:Idempotency:HashKey"] = Convert.ToBase64String(new byte[32]),
                ["Identity:Idempotency:ActiveOutcomeKeyId"] = "new",
                ["Identity:Idempotency:OutcomeKeys:new"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
            }).Build());

        Assert.Throws<CryptographicException>(() => configured.RestoreSuccess(outcome, typeof(Result<string>)));
    }

    [Fact]
    public void RotatedOutcomeKeyShouldReadPreviousOutcomeWithStableClaim()
    {
        var first = ConfiguredProtector("old");
        var rotated = ConfiguredProtector("new");
        var encrypted = first.ProtectSuccess(ResultFactory.Success("completed"));

        var restored = (Result<string>)rotated.RestoreSuccess(encrypted, typeof(Result<string>));

        Assert.Equal("completed", restored.Value);
    }

    [Fact]
    public void RotatedOutcomeKeyShouldPreserveKeyHashAndFingerprint()
    {
        var context = new HttpIdempotencyContext("identity.login", "0123456789abcdefghijkl", Guid.Empty, Guid.Empty);
        var request = new { Email = "user@example.test" };

        Assert.Equal(ConfiguredProtector("old").CreateRequest(request, context),
            ConfiguredProtector("new").CreateRequest(request, context));
    }

    private static IdentityHttpIdempotencyProtector ConfiguredProtector(string activeKey) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Idempotency:HashKey"] = Convert.ToBase64String(new byte[32]),
            ["Identity:Idempotency:ActiveOutcomeKeyId"] = activeKey,
            ["Identity:Idempotency:OutcomeKeys:old"] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
            ["Identity:Idempotency:OutcomeKeys:new"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
        }).Build());

    [Fact]
    public void SameOperationScopeAndKeyShouldProduceSameProtectedKey()
    {
        var context = new HttpIdempotencyContext("identity.login", "0123456789abcdefghijkl", Guid.Empty, Guid.Empty);
        var request = new { Email = "user@example.test", Password = "Password!123" };

        var first = _protector.CreateRequest(request, context);
        var second = _protector.CreateRequest(request, context);

        Assert.Equal(first.KeyHash, second.KeyHash);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void SameKeyWithDifferentBodyShouldHaveDifferentFingerprint()
    {
        var context = new HttpIdempotencyContext("identity.login", "0123456789abcdefghijkl", Guid.Empty, Guid.Empty);

        var first = _protector.CreateRequest(new { Email = "user@example.test", Password = "Password!123" }, context);
        var second = _protector.CreateRequest(new { Email = "user@example.test", Password = "OtherPassword!123" }, context);

        Assert.Equal(first.KeyHash, second.KeyHash);
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void SessionOutcomeShouldEncryptAndRestoreRefreshToken()
    {
        var session = new IdentitySessionResponse(
            "access-token",
            "refresh-token",
            900,
            "Bearer",
            null!,
            null!);

        var encrypted = _protector.ProtectSuccess(ResultFactory.Success(session));
        var restored = (Result<IdentitySessionResponse>)_protector.RestoreSuccess(
            encrypted,
            typeof(Result<IdentitySessionResponse>));

        Assert.True(restored.IsSuccess);
        Assert.Equal("refresh-token", restored.Value!.RefreshToken);
        Assert.DoesNotContain("refresh-token", System.Text.Encoding.UTF8.GetString(encrypted), StringComparison.Ordinal);
    }
}
