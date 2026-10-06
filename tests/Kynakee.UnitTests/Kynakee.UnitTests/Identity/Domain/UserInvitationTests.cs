using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Entities;
using Xunit;

namespace Kynakee.UnitTests.Identity.Domain;

public class UserInvitationTests
{
    [Fact]
    public void CreateShouldSetTwentyFourHourLifetime()
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(issuedAt);

        invitation.ExpiresAt.Should().Be(issuedAt.AddHours(24));
    }

    [Fact]
    public void AcceptShouldAllowOnlyOneUse()
    {
        var invitation = CreateInvitation(DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        invitation.Accept(now);

        var secondAcceptance = invitation.Accept(now.AddSeconds(1));

        secondAcceptance.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AcceptShouldRejectExpiredInvitation()
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(issuedAt);

        var result = invitation.Accept(issuedAt.AddHours(24));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RevokedInvitationShouldNotBeUsable()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(now);
        invitation.Revoke(now);

        invitation.IsUsableAt(now.AddMinutes(1)).Should().BeFalse();
    }

    private static UserInvitation CreateInvitation(DateTimeOffset issuedAt) => UserInvitation.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new string('B', 64),
        issuedAt).Value!;
}