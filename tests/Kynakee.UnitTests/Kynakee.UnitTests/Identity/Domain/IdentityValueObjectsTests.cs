using FluentAssertions;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Identity.Domain;

public class IdentityValueObjectsTests
{
    [Fact]
    public void EmailShouldNormalizeCaseAndWhitespace()
    {
        var result = Email.Create("  ADMIN@Example.COM  ");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("ADMIN@EXAMPLE.COM");
    }

    [Fact]
    public void EmailShouldRejectDisplayNameSyntax()
    {
        var result = Email.Create("Admin <admin@example.com>");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void PhoneNumberShouldAcceptE164Format()
    {
        var result = PhoneNumber.Create("+34612345678");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TenantSlugShouldNormalizeToLowercase()
    {
        var result = TenantSlug.Create("Reformas-Garcia");

        result.Value!.Value.Should().Be("reformas-garcia");
    }

    [Fact]
    public void TenantSlugShouldRejectRepeatedHyphens()
    {
        var result = TenantSlug.Create("reformas--garcia");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void TaxIdShouldAcceptValidSpanishNif()
    {
        var result = TaxId.Create("12345678z", "es");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TaxIdShouldRejectInvalidSpanishNifControlLetter()
    {
        var result = TaxId.Create("12345678A", "ES");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CompanySettingsShouldRejectPercentageAboveOneHundred()
    {
        var result = CompanySettings.Create(0, 101, 0, 0, 0, 0);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CompanyBrandingShouldRejectInvalidColor()
    {
        var result = CompanyBranding.Create(primaryColor: "blue");

        result.IsFailure.Should().BeTrue();
    }
}