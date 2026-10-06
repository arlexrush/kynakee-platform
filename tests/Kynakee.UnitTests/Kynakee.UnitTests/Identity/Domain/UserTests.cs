using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Events;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Identity.Domain;

public class UserTests
{
    [Fact]
    public void CreateShouldRaiseRegisteredEventForActiveUser()
    {
        var user = CreateActiveUser();

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegisteredDomainEvent>();
    }

    [Fact]
    public void ActivateShouldRequirePasswordHash()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            Email.Create("ada@example.com").Value,
            null,
            null,
            UserStatus.Inactive).Value!;

        var result = user.Activate();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ActivateShouldRaiseRegisteredEventAfterPasswordHashIsSet()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            Email.Create("ada@example.com").Value,
            null,
            null,
            UserStatus.Inactive).Value!;
        user.SetPasswordHash("hashed-password");

        var result = user.Activate();

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegisteredDomainEvent>();
    }

    [Fact]
    public void CreateShouldRejectEmptyTenantId()
    {
        var result = User.Create(
            Guid.Empty,
            "Ada",
            "Lovelace",
            Email.Create("ada@example.com").Value,
            null,
            "hashed-password");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CreateShouldRequirePasswordHashForActiveUser()
    {
        var result = User.Create(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            Email.Create("ada@example.com").Value,
            null,
            null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CreateShouldRaiseInvitationEventWithoutPasswordHash()
    {
        var result = User.Create(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            Email.Create("ada@example.com").Value,
            null,
            null,
            UserStatus.PendingVerification);

        result.Value!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserInvitedDomainEvent>();
    }

    [Fact]
    public void AcceptInvitationShouldActivateUserAndSetPasswordHash()
    {
        var user = CreateInvitedUser();

        var result = user.AcceptInvitation("hashed-password");

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        user.PasswordHash.Should().Be("hashed-password");
    }

    [Fact]
    public void AcceptInvitationShouldRejectSecondAcceptance()
    {
        var user = CreateInvitedUser();
        user.AcceptInvitation("hashed-password");

        var result = user.AcceptInvitation("another-hash");

        result.IsFailure.Should().BeTrue();
    }

    private static User CreateActiveUser() => User.Create(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        Email.Create("ada@example.com").Value,
        null,
        "hashed-password").Value!;

    private static User CreateInvitedUser() => User.Create(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        Email.Create("ada@example.com").Value,
        null,
        null,
        UserStatus.PendingVerification).Value!;
}