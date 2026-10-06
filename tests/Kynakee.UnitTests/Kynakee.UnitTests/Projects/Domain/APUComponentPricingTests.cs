using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class APUComponentPricingTests
{
    [Fact]
    public void CreateSuccessfulWithValidDataShouldNormalizeProviderAndSetPrice()
    {
        var queriedAt = DateTime.UtcNow.AddMinutes(-1);

        var result = APUComponentPricing.CreateSuccessful(
            Guid.NewGuid(),
            APUComponentId.New(),
            new Money(12.5m),
            MeasurementUnit.Kilogram,
            2,
            "  Supplier  ",
            PricingSource.McpProvider,
            Confidence.High,
            false,
            queriedAt: queriedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasPrice.Should().BeTrue();
        result.Value.ProviderName.Should().Be("Supplier");
        result.Value.UnitPrice!.Amount.Should().Be(12.5m);
        result.Value.QuotedUnit.Should().Be(MeasurementUnit.Kilogram);
        result.Value.UnitRevision.Should().Be(2);
        result.Value.QueriedAt.Should().Be(queriedAt);
    }

    [Fact]
    public void CreateSuccessfulWithoutTenantShouldReturnValidationError()
    {
        var result = APUComponentPricing.CreateSuccessful(
            Guid.Empty,
            APUComponentId.New(),
            new Money(1m),
            MeasurementUnit.Unit,
            0,
            null,
            PricingSource.McpProvider,
            Confidence.High,
            false);

        result.Error!.Code.Should().Be("PROJ_APU_PRICING_TENANT_REQUIRED");
    }

    [Fact]
    public void CreateSuccessfulWithoutComponentShouldReturnValidationError()
    {
        var result = APUComponentPricing.CreateSuccessful(
            Guid.NewGuid(),
            default,
            new Money(1m),
            MeasurementUnit.Unit,
            0,
            null,
            PricingSource.McpProvider,
            Confidence.High,
            false);

        result.Error!.Code.Should().Be("PROJ_APU_PRICING_COMPONENT_REQUIRED");
    }

    [Fact]
    public void CreateSuccessfulWithoutSourceShouldReturnValidationError()
    {
        var result = APUComponentPricing.CreateSuccessful(
            Guid.NewGuid(),
            APUComponentId.New(),
            new Money(1m),
            MeasurementUnit.Unit,
            0,
            null,
            PricingSource.None,
            Confidence.High,
            false);

        result.Error!.Code.Should().Be("PROJ_APU_PRICING_SOURCE_REQUIRED");
    }

    [Fact]
    public void CreateSuccessfulWithNegativeUnitRevisionShouldReturnValidationError()
    {
        var result = APUComponentPricing.CreateSuccessful(
            Guid.NewGuid(),
            APUComponentId.New(),
            new Money(1m),
            MeasurementUnit.Unit,
            -1,
            null,
            PricingSource.McpProvider,
            Confidence.High,
            false);

        result.Error!.Code.Should().Be("PROJ_APU_PRICING_UNIT_INVALID");
    }

    [Fact]
    public void CreateUnavailableWithValidDataShouldTrimReasonAndMarkFallbackBySource()
    {
        var result = APUComponentPricing.CreateUnavailable(
            Guid.NewGuid(),
            APUComponentId.New(),
            PricingSource.AlternativeSource,
            "  No quote available  ",
            MeasurementUnit.Kilogram,
            1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PricingStatus.Unavailable);
        result.Value.HasPrice.Should().BeFalse();
        result.Value.FailureReason.Should().Be("No quote available");
        result.Value.IsFallback.Should().BeTrue();
        result.Value.Confidence.Value.Should().Be(0m);
        result.Value.UnitPrice.Should().BeNull();
    }

    [Fact]
    public void CreateUnavailableFromMcpProviderShouldNotMarkFallback()
    {
        var result = APUComponentPricing.CreateUnavailable(
            Guid.NewGuid(),
            APUComponentId.New(),
            PricingSource.McpProvider,
            "Unavailable",
            MeasurementUnit.Kilogram,
            0);

        result.Value!.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void CreateUnavailableWithoutFailureReasonShouldReturnValidationError()
    {
        var result = APUComponentPricing.CreateUnavailable(
            Guid.NewGuid(),
            APUComponentId.New(),
            PricingSource.McpProvider,
            "  ",
            MeasurementUnit.Kilogram,
            0);

        result.Error!.Code.Should().Be("PROJ_APU_PRICING_FAILURE_REASON_REQUIRED");
    }

    [Fact]
    public void CreateUnavailableWithoutSourceShouldReturnValidationError()
    {
        var result = APUComponentPricing.CreateUnavailable(
            Guid.NewGuid(),
            APUComponentId.New(),
            PricingSource.None,
            "Unavailable",
            MeasurementUnit.Kilogram,
            0);

        result.Error!.Code.Should().Be("PROJ_APU_PRICING_SOURCE_REQUIRED");
    }

    [Fact]
    public void AuxiliaryCostApplyWithValidDataShouldSetUnitPrice()
    {
        var pricing = CreateSuccessfulPricing();

        var result = APUComponentPricing.AuxiliaryCostApply(new Money(3.5m), pricing);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UnitPrice!.Amount.Should().Be(3.5m);
    }

    [Fact]
    public void AuxiliaryCostApplyWithoutCostShouldReturnValidationError()
    {
        var result = APUComponentPricing.AuxiliaryCostApply(null!, CreateSuccessfulPricing());

        result.Error!.Code.Should().Be("PROJ_APU_PRICING_AUXILIARY_COST_REQUIRED");
    }

    [Fact]
    public void AuxiliaryCostApplyWithoutComponentPricingShouldReturnValidationError()
    {
        var result = APUComponentPricing.AuxiliaryCostApply(new Money(1m), null!);

        result.Error!.Code.Should().Be("PROJ_APU_PRICING_COMPONENT_REQUIRED");
    }

    private static APUComponentPricing CreateSuccessfulPricing() =>
        APUComponentPricing.CreateSuccessful(
            Guid.NewGuid(),
            APUComponentId.New(),
            new Money(2m),
            MeasurementUnit.Kilogram,
            0,
            null,
            PricingSource.McpProvider,
            Confidence.High,
            false).Value!;
}
