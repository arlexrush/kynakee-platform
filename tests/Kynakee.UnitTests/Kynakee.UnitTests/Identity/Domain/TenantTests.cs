using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Events;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Identity.Domain;

public class TenantTests
{
    [Fact]
    public void CreateShouldUseTenantIdAsItsOwnTenantId()
    {
        var tenant = CreateTenant();

        tenant.TenantId.Should().Be(tenant.Id);
    }

    [Fact]
    public void CreateShouldRaiseTenantCreatedEvent()
    {
        var tenant = CreateTenant();

        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantCreatedDomainEvent>();
    }

    [Fact]
    public void CreateShouldRejectMissingName()
    {
        var result = Tenant.Create(
            " ",
            TenantSlug.Create("tenant").Value,
            TenantType.Company,
            null,
            null,
            "starter");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateProfileShouldRejectTaxCountryMismatch()
    {
        var tenant = CreateTenant();
        var taxId = TaxId.Create("GB123456789", "GB").Value;
        var address = Address.Create("ES").Value;

        var result = tenant.UpdateProfile(
            "Kynakee Obras",
            TenantSlug.Create("kynakee-obras").Value,
            taxId,
            address,
            "starter");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("IDENTITY_TAX_COUNTRY_MISMATCH");
    }

    [Fact]
    public void UpdateProfileShouldUpdateNameAndPlanWhenValid()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdateProfile(
            "Kynakee Construcción",
            TenantSlug.Create("kynakee-construccion").Value,
            null,
            Address.Create("ES").Value,
            "pro");

        result.IsSuccess.Should().BeTrue();
        tenant.Name.Should().Be("Kynakee Construcción");
        tenant.PlanId.Should().Be("pro");
    }

    [Fact]
    public void CancelledTenantShouldRejectSettingsChanges()
    {
        var tenant = CreateTenant();
        tenant.ChangeStatus(TenantStatus.Cancelled);

        var result = tenant.UpdateSettings(CompanySettings.Default);

        result.IsFailure.Should().BeTrue();
    }

    private static Tenant CreateTenant() => Tenant.Create(
        "Kynakee Obras",
        TenantSlug.Create("kynakee-obras").Value,
        TenantType.Company,
        null,
        null,
        "starter").Value!;
}