using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class APUAssignmentTests
{
    [Fact]
    public void CreateWithoutComponentsShouldReturnValidationError()
    {
        var result = APUAssignment.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            WorkItemId.New(),
            MeasurementUnit.SquareMeter,
            APUTemplateId.New(),
            APUSource.Cached,
            [],
            Confidence.High);

        result.Error!.Code.Should().Be("PROJ_APU_COMPONENTS_REQUIRED");
    }

    [Fact]
    public void CreateWithNegativeMaterialWasteShouldReturnValidationError()
    {
        var result = APUAssignment.Create(
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
                -1m,
                false,
                2m)],
            Confidence.High);

        result.Error!.Code.Should().Be("PROJ_APU_MATERIAL_WASTE_INVALID");
    }

    [Fact]
    public void CreateWithMaterialComponentShouldTrimItsDescription()
    {
        var result = APUAssignment.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            WorkItemId.New(),
            MeasurementUnit.SquareMeter,
            APUTemplateId.New(),
            APUSource.Cached,
            [new MaterialComponentDefinition(
                Guid.NewGuid(),
                "  Cement  ",
                MeasurementUnit.Kilogram,
                0m,
                false,
                1m)],
            Confidence.High);

        result.Value!.Components.Single().Description.Should().Be("Cement");
    }

    [Fact]
    public void CalculateUnitPriceWithoutComponentPricingShouldReturnConflict()
    {
        var assignment = CreateAssignment(includePricing: false);

        var result = assignment.CalculateUnitPrice();

        result.Error!.Code.Should().Be("PROJ_APU_DIRECT_COMPONENT_PRICING_REQUIRED");
    }

    [Fact]
    public void CalculateUnitPriceShouldApplyMaterialWaste()
    {
        var assignment = CreateAssignment();

        var result = assignment.CalculateUnitPrice();

        result.Value!.DirectUnitCost.Amount.Should().Be(8.8m);
    }

    [Fact]
    public void CalculateUnitPriceWithoutAuxiliaryComponentsShouldReturnZeroAuxiliaryCost()
    {
        var assignment = CreateAssignment();

        var result = assignment.CalculateUnitPrice();

        result.Value!.AuxiliaryUnitCost.Amount.Should().Be(0m);
    }

    private static APUAssignment CreateAssignment(
        bool includePricing = true)
    {
        var tenantId = Guid.NewGuid();
        var assignment = APUAssignment.Create(
            tenantId,
            ProjectId.New(),
            WorkItemId.New(),
            MeasurementUnit.SquareMeter,
            APUTemplateId.New(),
            APUSource.Cached,
            [new MaterialComponentDefinition(
                Guid.NewGuid(),
                "Cement",
                MeasurementUnit.Kilogram,
                10m,
                false,
                2m)],
            Confidence.High).Value!;

        if (includePricing)
        {
            var component = assignment.Components.Single();
            var pricing = APUComponentPricing.CreateSuccessful(
                tenantId,
                component.Id,
                new Money(4m),
                component.Unit,
                component.UnitRevision,
                "Provider",
                PricingSource.McpProvider,
                Confidence.High,
                false).Value!;

            component.AddPricing(pricing);
        }

        return assignment;
    }
}
