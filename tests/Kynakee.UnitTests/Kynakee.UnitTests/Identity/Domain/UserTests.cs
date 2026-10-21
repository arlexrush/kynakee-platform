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

    [Fact]
    public void PrepareRegistrationInactiveUserWithPasswordRemainsPending()
    {
        var user = CreateRegistrationUser();

        user.PrepareRegistration();

        user.Status.Should().Be(UserStatus.PendingVerification);
    }

    [Fact]
    public void PrepareRegistrationNewOwnerDoesNotRaiseInvitationEvent()
    {
        var user = CreateRegistrationUser();

        user.PrepareRegistration();

        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void PrepareRegistrationWithoutPasswordIsRejected()
    {
        var user = User.Create(Guid.NewGuid(), "Ada", "Lovelace", Email.Create("ada@example.com").Value,
            null, null, UserStatus.Inactive).Value!;

        var result = user.PrepareRegistration();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ConfirmRegistrationPendingOwnerBecomesActive()
    {
        var user = CreateRegistrationUser();
        user.PrepareRegistration();

        user.ConfirmRegistration();

        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void ConfirmRegistrationPendingOwnerRaisesRegisteredEvent()
    {
        var user = CreateRegistrationUser();
        user.PrepareRegistration();

        user.ConfirmRegistration();

        user.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserRegisteredDomainEvent>();
    }

    [Fact]
    public void ConfirmRegistrationSecondConfirmationIsRejected()
    {
        var user = CreateRegistrationUser();
        user.PrepareRegistration();
        user.ConfirmRegistration();

        var result = user.ConfirmRegistration();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ConfirmRegistrationDisabledOwnerIsRejected()
    {
        var user = CreateRegistrationUser();
        user.PrepareRegistration();
        user.Deactivate();

        var result = user.ConfirmRegistration();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ConfirmRegistrationInviteeWithoutPasswordIsRejected()
    {
        var user = CreateInvitedUser();

        var result = user.ConfirmRegistration();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RecoverPasswordActiveUserReplacesPasswordHash()
    {
        var user = CreateActiveUser();

        user.RecoverPassword("replacement-hash");

        user.PasswordHash.Should().Be("replacement-hash");
    }

    [Fact]
    public void RecoverPasswordDisabledUserIsRejected()
    {
        var user = CreateActiveUser();
        user.Deactivate();

        var result = user.RecoverPassword("replacement-hash");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RecoverPasswordPendingOwnerIsRejected()
    {
        var user = CreateRegistrationUser();
        user.PrepareRegistration();

        var result = user.RecoverPassword("replacement-hash");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RecoverPasswordEmptyHashIsRejected()
    {
        var user = CreateActiveUser();

        var result = user.RecoverPassword(string.Empty);

        result.IsFailure.Should().BeTrue();
    }

    private static User CreateRegistrationUser()
    {
        var user = User.Create(Guid.NewGuid(), "Ada", "Lovelace", Email.Create("ada@example.com").Value,
            null, null, UserStatus.Inactive).Value!;
        user.SetPasswordHash("hashed-password");
        return user;
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