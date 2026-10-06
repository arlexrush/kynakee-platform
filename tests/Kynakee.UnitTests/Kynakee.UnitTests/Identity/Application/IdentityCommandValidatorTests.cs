using FluentAssertions;
using Kynakee.Modules.Identity.Application.Commands.Login;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Application.Commands.RefreshSession;
using Kynakee.Modules.Identity.Application.Commands.AcceptInvitation;
using Xunit;

namespace Kynakee.UnitTests.Identity.Application;

public class IdentityCommandValidatorTests
{
    [Fact]
    public void RegisterTenantOwnerValidatorShouldRejectWeakPassword()
    {
        var validator = new RegisterTenantOwnerCommandValidator();
        var command = new RegisterTenantOwnerCommand(
            "Ada",
            "Lovelace",
            "ada@example.com",
            "weak",
            "+34612345678",
            "Analytical Engines",
            Kynakee.Modules.Identity.Domain.ValueObjects.TenantType.Company,
            "GB123456789",
            "GB",
            "starter",
            "web");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Password");
    }

    [Fact]
    public void RegisterTenantOwnerValidatorShouldAcceptStrongPasswordAndValidInput()
    {
        var validator = new RegisterTenantOwnerCommandValidator();
        var command = new RegisterTenantOwnerCommand(
            "Ada",
            "Lovelace",
            "ada@example.com",
            "ValidPassword#123",
            "+34612345678",
            "Analytical Engines",
            Kynakee.Modules.Identity.Domain.ValueObjects.TenantType.Company,
            "GB123456789",
            "GB",
            "starter",
            "web");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void LoginValidatorShouldRejectUnsupportedChannel()
    {
        var validator = new LoginCommandValidator();
        var command = new LoginCommand("ada@example.com", "Password#123456", "unknown");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Channel");
    }

    [Fact]
    public void RefreshSessionValidatorShouldRejectEmptyToken()
    {
        var validator = new RefreshSessionCommandValidator();
        var result = validator.Validate(new RefreshSessionCommand(string.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "RefreshToken");
    }

    [Fact]
    public void AcceptInvitationValidatorShouldRejectWeakPassword()
    {
        var validator = new AcceptInvitationCommandValidator();
        var result = validator.Validate(new AcceptInvitationCommand("invitation-token", "weak"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Password");
    }
}