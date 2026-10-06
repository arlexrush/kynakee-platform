using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Entities;
using Xunit;

namespace Kynakee.UnitTests.Identity.Domain;

public class RefreshTokenTests
{
    [Fact]
    public void CreateShouldSetThirtyDayLifetimeAndPersistOnlyHash()
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), issuedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExpiresAt.Should().Be(issuedAt.AddDays(30));
        result.Value.TokenHash.Should().HaveLength(64);
    }

    [Fact]
    public void CreateShouldRejectNonSha256TokenHash()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "raw-token", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RevokeShouldMarkTokenAndReplacement()
    {
        var token = CreateToken();
        var replacementId = Guid.NewGuid();

        var result = token.Revoke(DateTimeOffset.UtcNow, replacementId);

        result.IsSuccess.Should().BeTrue();
        token.ReplacedByTokenId.Should().Be(replacementId);
        token.IsUsableAt(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void RevokeShouldRejectAlreadyRevokedToken()
    {
        var token = CreateToken();
        token.Revoke(DateTimeOffset.UtcNow);

        var result = token.Revoke(DateTimeOffset.UtcNow.AddSeconds(1));

        result.IsFailure.Should().BeTrue();
    }

    private static RefreshToken CreateToken() => RefreshToken.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new string('A', 64),
        DateTimeOffset.UtcNow).Value!;
}