using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class APUComponentPricingSelectionTests
{
    [Fact]
    public void CurrentPricingShouldPreferMcpProviderOverNewerCachedPrice()
    {
        var component = CreateMaterialComponent();
        var mcpPrice = CreateAvailablePricing(
            component,
            PricingSource.McpProvider,
            5m,
            DateTime.UtcNow.AddDays(-1));
        var cachedPrice = CreateAvailablePricing(
            component,
            PricingSource.CachedPrice,
            9m,
            DateTime.UtcNow);

        component.AddPricing(cachedPrice).IsSuccess.Should().BeTrue();
        component.AddPricing(mcpPrice).IsSuccess.Should().BeTrue();

        component.CurrentPricing.Should().BeSameAs(mcpPrice);
    }

    [Fact]
    public void CurrentPricingShouldSelectNewestMcpProviderPrice()
    {
        var component = CreateMaterialComponent();
        var olderPrice = CreateAvailablePricing(
            component,
            PricingSource.McpProvider,
            5m,
            DateTime.UtcNow.AddDays(-1));
        var newerPrice = CreateAvailablePricing(
            component,
            PricingSource.McpProvider,
            7m,
            DateTime.UtcNow);

        component.AddPricing(olderPrice).IsSuccess.Should().BeTrue();
        component.AddPricing(newerPrice).IsSuccess.Should().BeTrue();

        component.CurrentPricing.Should().BeSameAs(newerPrice);
    }

    [Fact]
    public void CurrentPricingShouldPreferCachedPriceOverAlternativeSource()
    {
        var component = CreateMaterialComponent();
        var alternativePrice = CreateAvailablePricing(
            component,
            PricingSource.AlternativeSource,
            5m,
            DateTime.UtcNow);
        var cachedPrice = CreateAvailablePricing(
            component,
            PricingSource.CachedPrice,
            7m,
            DateTime.UtcNow.AddDays(-1));

        component.AddPricing(alternativePrice).IsSuccess.Should().BeTrue();
        component.AddPricing(cachedPrice).IsSuccess.Should().BeTrue();

        component.CurrentPricing.Should().BeSameAs(cachedPrice);
    }

    [Fact]
    public void CurrentPricingShouldUseAlternativePriceWhenNoPreferredSourceExists()
    {
        var component = CreateMaterialComponent();
        var alternativePrice = CreateAvailablePricing(
            component,
            PricingSource.AlternativeSource,
            5m,
            DateTime.UtcNow);

        component.AddPricing(alternativePrice).IsSuccess.Should().BeTrue();

        component.CurrentPricing.Should().BeSameAs(alternativePrice);
    }

    [Fact]
    public void CurrentPricingShouldIgnoreUnavailablePricing()
    {
        var component = CreateMaterialComponent();
        var unavailablePricing = APUComponentPricing.CreateUnavailable(
            component.TenantId,
            component.Id,
            PricingSource.McpProvider,
            "No quote available",
            component.Unit,
            component.UnitRevision).Value!;

        component.AddPricing(unavailablePricing).IsSuccess.Should().BeTrue();

        component.CurrentPricing.Should().BeNull();
    }

    private static MaterialComponent CreateMaterialComponent()
    {
        var assignment = APUAssignment.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            WorkItemId.New(),
            MeasurementUnit.SquareMeter,
            APUTemplateId.New(),
            APUSource.Cached,
            [new MaterialComponentDefinition(
                Guid.NewGuid(),
                "Cement",
                MeasurementUnit.Kilogram,
                0m,
                false,
                1m)],
            Confidence.High).Value!;

        return (MaterialComponent)assignment.Components.Single();
    }

    private static APUComponentPricing CreateAvailablePricing(
        MaterialComponent component,
        PricingSource source,
        decimal amount,
        DateTime queriedAt) =>
        APUComponentPricing.CreateSuccessful(
            component.TenantId,
            component.Id,
            new Money(amount),
            component.Unit,
            component.UnitRevision,
            null,
            source,
            Confidence.High,
            false,
            queriedAt: queriedAt).Value!;
}
