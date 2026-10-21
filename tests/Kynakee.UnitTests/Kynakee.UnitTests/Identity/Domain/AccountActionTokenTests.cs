using Kynakee.Modules.Identity.Domain.Entities;
using Xunit;

namespace Kynakee.UnitTests.Identity.Domain;

public sealed class AccountActionTokenTests
{
    [Theory]
    [InlineData(AccountActionPurpose.VerifyEmail)]
    [InlineData(AccountActionPurpose.ResetPassword)]
    public void TokenMatchingPurposeShouldBeUsable(AccountActionPurpose purpose)
    {
        var now = DateTimeOffset.UtcNow;
        var token = Create(now, purpose);

        Assert.True(token.IsUsableAt(purpose, now));
    }

    [Fact]
    public void TokenDifferentPurposeShouldRejectConsumption()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Create(now);

        Assert.True(token.Consume(AccountActionPurpose.ResetPassword, now).IsFailure);
    }

    [Fact]
    public void TokenConsumedShouldRejectSecondUse()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Create(now);
        token.Consume(AccountActionPurpose.VerifyEmail, now);

        Assert.True(token.Consume(AccountActionPurpose.VerifyEmail, now).IsFailure);
    }

    [Fact]
    public void TokenExpiredShouldRejectConsumption()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Create(now);

        Assert.True(token.Consume(AccountActionPurpose.VerifyEmail, now.AddHours(1)).IsFailure);
    }

    [Fact]
    public void TokenBeforeIssueShouldRejectConsumption()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Create(now);

        Assert.True(token.Consume(AccountActionPurpose.VerifyEmail, now.AddSeconds(-1)).IsFailure);
    }

    [Fact]
    public void TokenRevokedShouldRejectConsumption()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Create(now);
        token.Revoke(now);

        Assert.True(token.Consume(AccountActionPurpose.VerifyEmail, now).IsFailure);
    }

    [Fact]
    public void TokenConsumedShouldRejectRevocation()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Create(now);
        token.Consume(AccountActionPurpose.VerifyEmail, now);

        Assert.True(token.Revoke(now).IsFailure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-digest")]
    public void TokenInvalidDigestShouldRejectCreation(string digest)
    {
        var now = DateTimeOffset.UtcNow;

        Assert.True(AccountActionToken.Create(Guid.NewGuid(), Guid.NewGuid(), digest,
            AccountActionPurpose.VerifyEmail, now, now.AddHours(1)).IsFailure);
    }

    [Fact]
    public void TokenInvalidExpiryShouldRejectCreation()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.True(AccountActionToken.Create(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64),
            AccountActionPurpose.VerifyEmail, now, now).IsFailure);
    }

    private static AccountActionToken Create(DateTimeOffset now, AccountActionPurpose purpose = AccountActionPurpose.VerifyEmail) =>
        AccountActionToken.Create(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), purpose, now, now.AddHours(1)).Value!;
}
