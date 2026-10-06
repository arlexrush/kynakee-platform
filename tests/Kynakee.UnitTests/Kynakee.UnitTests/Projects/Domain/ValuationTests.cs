using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class ValuationTests
{
    [Fact]
    public void CreateWithoutAssignmentsShouldReturnValidationError()
    {
        var result = Valuation.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            [],
            new Dictionary<WorkItemId, decimal>());

        result.Error!.Code.Should().Be("PROJ_VALUATION_ASSIGNMENTS_REQUIRED");
    }

    [Fact]
    public void CreateWithMismatchedWorkItemSetShouldReturnValidationError()
    {
        var tenantId = Guid.NewGuid();
        var assignment = CreateAssignment(tenantId, ProjectId.New());

        var result = Valuation.Create(
            tenantId,
            assignment.ProjectId,
            [assignment],
            new Dictionary<WorkItemId, decimal>());

        result.Error!.Code.Should().Be("PROJ_VALUATION_WORKITEM_SET_MISMATCH");
    }

    [Fact]
    public void CreateWithNonPositiveWorkItemQuantityShouldReturnValidationError()
    {
        var tenantId = Guid.NewGuid();
        var assignment = CreateAssignment(tenantId, ProjectId.New());

        var result = Valuation.Create(
            tenantId,
            assignment.ProjectId,
            [assignment],
            new Dictionary<WorkItemId, decimal>
            {
                [assignment.WorkItemId] = 0m
            });

        result.Error!.Code.Should().Be("PROJ_VALUATION_WORKITEM_QUANTITY_REQUIRED");
    }

    [Fact]
    public void CreateWithAssignmentFromDifferentTenantShouldReturnConflict()
    {
        var assignment = CreateAssignment(Guid.NewGuid(), ProjectId.New());

        var result = Valuation.Create(
            Guid.NewGuid(),
            assignment.ProjectId,
            [assignment],
            new Dictionary<WorkItemId, decimal>
            {
                [assignment.WorkItemId] = 1m
            });

        result.Error!.Code.Should().Be("PROJ_VALUATION_ASSIGNMENT_TENANT_MISMATCH");
    }

    [Fact]
    public void CreateWithPricedAssignmentShouldCalculateDirectCost()
    {
        var tenantId = Guid.NewGuid();
        var assignment = CreateAssignment(tenantId, ProjectId.New());

        var result = Valuation.Create(
            tenantId,
            assignment.ProjectId,
            [assignment],
            new Dictionary<WorkItemId, decimal>
            {
                [assignment.WorkItemId] = 3m
            });

        result.Value!.DirectCost.Amount.Should().Be(26.4m);
    }

    [Fact]
    public void CreateWithPricedAssignmentShouldCreateCompleteValuation()
    {
        var tenantId = Guid.NewGuid();
        var assignment = CreateAssignment(tenantId, ProjectId.New());

        var result = Valuation.Create(
            tenantId,
            assignment.ProjectId,
            [assignment],
            new Dictionary<WorkItemId, decimal>
            {
                [assignment.WorkItemId] = 3m
            });

        result.Value!.IsComplete.Should().BeTrue();
    }

    private static APUAssignment CreateAssignment(
        Guid tenantId,
        ProjectId projectId)
    {
        var assignment = APUAssignment.Create(
            tenantId,
            projectId,
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

        return assignment;
    }
}
