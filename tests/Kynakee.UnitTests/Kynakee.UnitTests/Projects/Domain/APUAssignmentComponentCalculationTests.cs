using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class APUAssignmentComponentCalculationTests
{
    [Fact]
    public void CalculateUnitPriceShouldCalculateLaborCostFromCrewSizeAndProductivity()
    {
        var assignment = CreateAssignment(new LaborComponentDefinition(
            Guid.NewGuid(),
            "Mason",
            MeasurementUnit.Hour,
            "Masonry",
            2m,
            4m));
        AddPricing(assignment, 30m);

        var result = assignment.CalculateUnitPrice();

        result.IsSuccess.Should().BeTrue();
        result.Value!.LaborSubtotal.Amount.Should().Be(15m);
        result.Value.ComponentSnapshots.Should().ContainSingle();
    }

    [Fact]
    public void CalculateUnitPriceShouldReturnValidationErrorWhenLaborTechnicalDataIsInvalid()
    {
        var assignment = CreateAssignmentResult(new LaborComponentDefinition(
            Guid.NewGuid(),
            "Mason",
            MeasurementUnit.Hour,
            "Masonry",
            0m,
            4m));

        assignment.Error!.Code.Should().Be("PROJ_APU_LABOR_TECHNICAL_DATA_INVALID");
    }

    [Fact]
    public void CalculateUnitPriceShouldCalculateEquipmentCostFromCountAndHours()
    {
        var assignment = CreateAssignmentResult(new EquipmentComponentDefinition(
            Guid.NewGuid(),
            "Excavator",
            MeasurementUnit.Hour,
            "Earthmoving",
            2m,
            3m));
        AddPricing(assignment.Value!, 40m);

        var result = assignment.Value!.CalculateUnitPrice();

        result.IsSuccess.Should().BeTrue();
        result.Value!.EquipmentSubtotal.Amount.Should().Be(240m);
        result.Value.ComponentSnapshots.Should().ContainSingle();
    }

    [Fact]
    public void CalculateUnitPriceShouldReturnValidationErrorWhenEquipmentTechnicalDataIsInvalid()
    {
        const string expectedErrorCode = "PROJ_APU_EQUIPMENT_TECHNICAL_DATA_INVALID";
        var assignment = CreateAssignmentResult(new EquipmentComponentDefinition(
            Guid.NewGuid(),
            "Excavator",
            MeasurementUnit.Hour,
            "Earthmoving",
            0m,
            3m));

        assignment.Error!.Code.Should().Be(expectedErrorCode);
    }

    [Fact]
    public void CalculateUnitPriceShouldCalculateSubcontractCostFromQuantity()
    {
        var assignment = CreateAssignment(new SubcontractComponentDefinition(
            Guid.NewGuid(),
            "Concrete placement",
            MeasurementUnit.CubicMeter,
            "Supply and placement",
            2m));
        AddPricing(assignment, 75m);

        var result = assignment.CalculateUnitPrice();

        result.IsSuccess.Should().BeTrue();
        result.Value!.SubcontractSubtotal.Amount.Should().Be(150m);
    }

    [Fact]
    public void CreateSubcontractWithMissingDescriptionShouldReturnValidationError()
    {
        var result = CreateAssignmentResult(new SubcontractComponentDefinition(
            Guid.NewGuid(),
            " ",
            MeasurementUnit.CubicMeter,
            "Supply and placement",
            2m));

        result.Error!.Code.Should().Be("PROJ_APU_SUBCONTRACT_DESCRIPTION_INVALID");
    }

    [Fact]
    public void CalculateUnitPriceShouldCalculateTransportCostPerTrip()
    {
        var assignment = CreateAssignment(new TransportComponentDefinition(
            Guid.NewGuid(),
            "Material delivery",
            MeasurementUnit.Ton,
            10m,
            5m,
            TransportRateBasis.PerTrip,
            2m,
            4m));
        AddPricing(assignment, 100m);

        var result = assignment.CalculateUnitPrice();

        result.IsSuccess.Should().BeTrue();
        result.Value!.TransportSubtotal.Amount.Should().Be(80m);
    }

    [Fact]
    public void CreateTransportWithMissingDescriptionShouldReturnValidationError()
    {
        var result = CreateAssignmentResult(new TransportComponentDefinition(
            Guid.NewGuid(),
            " ",
            MeasurementUnit.Ton,
            10m,
            5m,
            TransportRateBasis.PerTrip,
            2m,
            4m));

        result.Error!.Code.Should().Be("PROJ_APU_TRANSPORT_DESCRIPTION_INVALID");
    }

    [Fact]
    public void CalculateUnitPriceShouldCalculateTransportCostPerVehicleKilometer()
    {
        var assignment = CreateAssignment(new TransportComponentDefinition(
            Guid.NewGuid(),
            "Material delivery",
            MeasurementUnit.Ton,
            10m,
            5m,
            TransportRateBasis.PerVehicleKilometer,
            2m,
            4m));
        AddPricing(assignment, 100m);

        var result = assignment.CalculateUnitPrice();

        result.IsSuccess.Should().BeTrue();
        result.Value!.TransportSubtotal.Amount.Should().Be(1600m);
    }

    [Fact]
    public void CalculateUnitPriceShouldCalculateTransportCostPerTonKilometer()
    {
        var assignment = CreateAssignment(new TransportComponentDefinition(
            Guid.NewGuid(),
            "Material delivery",
            MeasurementUnit.Ton,
            10m,
            5m,
            TransportRateBasis.PerTonKilometer,
            2m,
            4m));
        AddPricing(assignment, 100m);

        var result = assignment.CalculateUnitPrice();

        result.IsSuccess.Should().BeTrue();
        result.Value!.TransportSubtotal.Amount.Should().Be(4000m);
    }

    [Fact]
    public void CalculateUnitPriceShouldCalculateTransportCostPerUnit()
    {
        var assignment = CreateAssignment(new TransportComponentDefinition(
            Guid.NewGuid(),
            "Material delivery",
            MeasurementUnit.Ton,
            10m,
            5m,
            TransportRateBasis.PerUnit,
            2m,
            4m));
        AddPricing(assignment, 100m);

        var result = assignment.CalculateUnitPrice();

        result.IsSuccess.Should().BeTrue();
        result.Value!.TransportSubtotal.Amount.Should().Be(400m);
    }

    [Fact]
    public void CalculateUnitPriceShouldReturnErrorWhenTransportQuantityIsInvalid()
    {
        var assignment = CreateAssignment(new TransportComponentDefinition(
            Guid.NewGuid(),
            "Material delivery",
            MeasurementUnit.Ton,
            10m,
            5m,
            TransportRateBasis.PerUnit,
            2m,
            0m));
        AddPricing(assignment, 100m);

        var result = assignment.CalculateUnitPrice();

        result.Error!.Code.Should().Be("PROJ_APU_TRANSPORT_CALCULATION_INVALID");
    }

    private static Kynakee.Modules.SharedKernel.Application.Result<APUAssignment> CreateAssignmentResult(
        APUComponentDefinition definition) =>
        APUAssignment.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            WorkItemId.New(),
            MeasurementUnit.SquareMeter,
            APUTemplateId.New(),
            APUSource.Cached,
            [definition],
            Confidence.High);

    private static APUAssignment CreateAssignment(APUComponentDefinition definition) =>
        CreateAssignmentResult(definition).Value!;

    private static void AddPricing(APUAssignment assignment, decimal unitPrice)
    {
        var component = assignment.Components.Single();
        var pricing = APUComponentPricing.CreateSuccessful(
            assignment.TenantId,
            component.Id,
            new Money(unitPrice),
            component.Unit,
            component.UnitRevision,
            "Provider",
            PricingSource.McpProvider,
            Confidence.High,
            false).Value!;

        component.AddPricing(pricing).IsSuccess.Should().BeTrue();
    }
}
