using FluentAssertions;
using Kynakee.Modules.Identity.Application.Commands.AcceptInvitation;
using Kynakee.Modules.Identity.Application.Commands.Login;
using Kynakee.Modules.Identity.Application.Commands.RefreshSession;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;
using Xunit;

namespace Kynakee.UnitTests.Identity.Application;

public class IdentityCommandValidatorTests
{
    [Fact]
    public void UpdateTenantProfileValidatorWithoutFiscalAddressShouldAcceptInput()
    {
        var validator = new UpdateTenantProfileCommandValidator();
        var command = new UpdateTenantProfileCommand(Guid.NewGuid(), "Tenant", "tenant", null, null,
            null, null, null, null, null, null, "starter", 0);

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "Region", null, null, null, null)]
    [InlineData("ES", null, null, null, null, null)]
    [InlineData("ES", null, null, "Madrid", "28001", null)]
    public void UpdateTenantProfileValidatorPartialFiscalAddressShouldRejectInput(
        string? country, string? region, string? province, string? municipality, string? postalCode, string? street)
    {
        var validator = new UpdateTenantProfileCommandValidator();
        var command = new UpdateTenantProfileCommand(Guid.NewGuid(), "Tenant", "tenant", null, null,
            country, region, province, municipality, postalCode, street, "starter", 0);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateTenantProfileValidatorCompleteFiscalAddressShouldAcceptInput()
    {
        var validator = new UpdateTenantProfileCommandValidator();
        var command = new UpdateTenantProfileCommand(Guid.NewGuid(), "Tenant", "tenant", null, null,
            "ES", null, null, "Madrid", "28001", "Calle Mayor 1", "starter", 0);

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

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
            "GB",
            "England",
            "Greater London",
            "London",
            "SW1A 1AA",
            "10 Downing Street",
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
            "GB",
            "England",
            "Greater London",
            "London",
            "SW1A 1AA",
            "10 Downing Street",
            "starter",
            "web");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisterTenantOwnerValidatorShouldAcceptInputWithoutFiscalAddress()
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
            null,
            null,
            null,
            null,
            null,
            null,
            "starter",
            "web");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisterTenantOwnerValidatorShouldRejectPartialFiscalAddress()
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
            "GB",
            null,
            null,
            null,
            null,
            null,
            "starter",
            "web");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "FiscalMunicipality");
        result.Errors.Should().Contain(error => error.PropertyName == "FiscalPostalCode");
        result.Errors.Should().Contain(error => error.PropertyName == "FiscalStreet");
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