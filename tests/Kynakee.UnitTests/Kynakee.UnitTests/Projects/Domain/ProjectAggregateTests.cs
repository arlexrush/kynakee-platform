using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.DataCapture;
using Kynakee.Modules.Projects.Domain.Entities.DataContext;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class ProjectAggregateTests
{
    [Fact]
    public void CreateWithEmptyTenantShouldReturnValidationError()
    {
        var result = Project.Create(
            "Project",
            new ClientInfo("Client"),
            new GeoLocation("ES"),
            ProjectChannel.Web,
            Guid.Empty);

        result.Error!.Code.Should().Be("PROJ_TENANT_REQUIRED");
    }

    [Fact]
    public void CreateWithBlankNameShouldReturnValidationError()
    {
        var result = Project.Create(
            " ",
            new ClientInfo("Client"),
            new GeoLocation("ES"),
            ProjectChannel.Web,
            Guid.NewGuid());

        result.Error!.Code.Should().Be("PROJ_NAME_REQUIRED");
    }

    [Fact]
    public void CreateShouldTrimProjectName()
    {
        var project = CreateProject(name: "  Renovation  ");

        project.Name.Should().Be("Renovation");
    }

    [Fact]
    public void CreateShouldStartInInitializationPhase()
    {
        var project = CreateProject();

        project.CurrentPhase.Should().Be(ProjectPhase.Initialization);
    }

    [Fact]
    public void CreateShouldRaiseProjectCreatedEvent()
    {
        var project = CreateProject();

        project.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void StartCaptureFromInitializationShouldAdvanceToCapture()
    {
        var project = CreateProject();

        project.StartCapture();

        project.CurrentPhase.Should().Be(ProjectPhase.Capture);
    }

    [Fact]
    public void StartCaptureOutsideInitializationShouldReturnConflict()
    {
        var project = CreateProject();
        project.StartCapture();

        var result = project.StartCapture();

        result.Error!.Code.Should().Be("PROJ_CAPTURE_INVALID_PHASE");
    }

    [Fact]
    public void CompleteCaptureFromDifferentTenantShouldReturnConflict()
    {
        var tenantId = Guid.NewGuid();
        var project = CreateProject(tenantId: tenantId);
        project.StartCapture();
        var capture = CaptureExpedient.Create(
            Guid.NewGuid(),
            project.Id).Value!;

        var result = project.CompleteCapture(capture);

        result.Error!.Code.Should().Be("PROJ_CAPTURE_TENANT_MISMATCH");
    }

    [Fact]
    public void CompleteCaptureShouldAdvanceToContext()
    {
        var project = CreateProject();
        project.StartCapture();
        var capture = CaptureExpedient.Create(
            project.TenantId,
            project.Id).Value!;

        project.CompleteCapture(capture);

        project.CurrentPhase.Should().Be(ProjectPhase.Context);
    }

    [Fact]
    public void SetContextForDifferentProjectShouldReturnConflict()
    {
        var project = CreateProjectAtContextPhase();
        var context = CreateProjectContext(
            project.TenantId,
            ProjectId.New());

        var result = project.SetContext(context);

        result.Error!.Code.Should().Be("PROJ_CONTEXT_PROJECT_MISMATCH");
    }

    [Fact]
    public void SetContextShouldAdvanceToScope()
    {
        var project = CreateProjectAtContextPhase();
        var context = CreateProjectContext(project.TenantId, project.Id);

        project.SetContext(context);

        project.CurrentPhase.Should().Be(ProjectPhase.Scope);
    }

    [Fact]
    public void AddWorkItemBeforeScopeShouldReturnConflict()
    {
        var project = CreateProject();
        var workItem = CreateWorkItem(project.TenantId, project.Id);

        var result = project.AddWorkItem(workItem);

        result.Error!.Code.Should().Be("PROJ_WORKITEM_INVALID_PHASE");
    }

    [Fact]
    public void AddWorkItemFromDifferentTenantShouldReturnConflict()
    {
        var project = CreateProjectAtScopePhase();
        var workItem = CreateWorkItem(Guid.NewGuid(), project.Id);

        var result = project.AddWorkItem(workItem);

        result.Error!.Code.Should().Be("PROJ_WORKITEM_TENANT_MISMATCH");
    }

    [Fact]
    public void AddWorkItemFromDifferentProjectShouldReturnConflict()
    {
        var project = CreateProjectAtScopePhase();
        var workItem = CreateWorkItem(project.TenantId, ProjectId.New());

        var result = project.AddWorkItem(workItem);

        result.Error!.Code.Should().Be("PROJ_WORKITEM_PROJECT_MISMATCH");
    }

    [Fact]
    public void AddWorkItemForProjectShouldAddItToAggregate()
    {
        var project = CreateProjectAtScopePhase();
        var workItem = CreateWorkItem(project.TenantId, project.Id);

        project.AddWorkItem(workItem);

        project.WorkItems.Should().ContainSingle().Which.Should().BeSameAs(workItem);
    }

    [Fact]
    public void UpdateQuantityForMissingWorkItemShouldReturnNotFound()
    {
        var project = CreateProjectAtScopePhase();

        var result = project.UpdateWorkItemQuantity(
            WorkItemId.New(),
            3m);

        result.Error!.Code.Should().Be("PROJ_WORKITEM_NOT_FOUND");
    }

    [Fact]
    public void UpdateQuantityWithNonPositiveValueShouldReturnValidationError()
    {
        var project = CreateProjectWithWorkItem(out var workItem);

        var result = project.UpdateWorkItemQuantity(workItem.Id, 0m);

        result.Error!.Code.Should().Be("PROJ_WORKITEM_QUANTITY_INVALID");
    }

    [Fact]
    public void UpdateQuantityShouldStoreNewQuantity()
    {
        var project = CreateProjectWithWorkItem(out var workItem);

        project.UpdateWorkItemQuantity(workItem.Id, 5m);

        project.WorkItems.Single().Quantity.Should().Be(5m);
    }

    [Fact]
    public void UpdateQuantityShouldMarkWorkItemAsModifiedByHuman()
    {
        var project = CreateProjectWithWorkItem(out var workItem);

        project.UpdateWorkItemQuantity(workItem.Id, 5m);

        project.WorkItems.Single().AIStatus.Should().Be(WorkItemAIStatus.ModifiedByHuman);
    }

    [Fact]
    public void UpdateDescriptionWithBlankValueShouldReturnValidationError()
    {
        var project = CreateProjectWithWorkItem(out var workItem);

        var result = project.UpdateWorkItemDescription(workItem.Id, " ");

        result.Error!.Code.Should().Be("PROJ_WORKITEM_DESCRIPTION_REQUIRED");
    }

    [Fact]
    public void UpdateDescriptionShouldTrimValue()
    {
        var project = CreateProjectWithWorkItem(out var workItem);

        project.UpdateWorkItemDescription(workItem.Id, "  Install flooring  ");

        project.WorkItems.Single().Description.Should().Be("Install flooring");
    }

    [Fact]
    public void StartProductionWithoutWorkItemsShouldReturnConflict()
    {
        var project = CreateProjectAtScopePhase();

        var result = project.StartProduction();

        result.Error!.Code.Should().Be("PROJ_PRODUCTION_WORKITEMS_REQUIRED");
    }

    [Fact]
    public void StartProductionWithWorkItemShouldAdvanceToProduction()
    {
        var project = CreateProjectWithWorkItem(out _);

        project.StartProduction();

        project.CurrentPhase.Should().Be(ProjectPhase.Production);
    }

    [Fact]
    public void CancelWithoutReasonShouldReturnValidationError()
    {
        var project = CreateProject();

        var result = project.Cancel(" ");

        result.Error!.Code.Should().Be("PROJ_PROJECT_CANCEL_REASON_REQUIRED");
    }

    [Fact]
    public void CancelWithReasonShouldSetCancelledStatus()
    {
        var project = CreateProject();

        project.Cancel(" No longer required ");

        project.Status.Should().Be(ProjectStatus.Cancelled);
    }

    [Fact]
    public void AddTokenConsumptionWithNegativeTokensShouldReturnValidationError()
    {
        var project = CreateProject();

        var result = project.AddTokenConsumption(
            ProjectPhase.Capture,
            -1,
            1m);

        result.Error!.Code.Should().Be("PROJ_TOKEN_CONSUMPTION_INVALID");
    }

    [Fact]
    public void AddTokenConsumptionShouldAccumulateTotals()
    {
        var project = CreateProject();
        project.AddTokenConsumption(ProjectPhase.Capture, 10, 2m);

        project.AddTokenConsumption(ProjectPhase.Capture, 5, 1m);

        project.TotalTokensConsumed.TotalTokens.Should().Be(15);
    }

    [Fact]
    public void AddTokenConsumptionShouldAccumulateCredits()
    {
        var project = CreateProject();

        project.AddTokenConsumption(ProjectPhase.Capture, 10, 2m);
        project.AddTokenConsumption(ProjectPhase.Capture, 5, 1m);

        project.TotalTokensConsumed.TotalCredits.Should().Be(3m);
    }

    private static Project CreateProject(
        Guid? tenantId = null,
        string name = "Residential project") =>
        Project.Create(
            name,
            new ClientInfo("Client"),
            new GeoLocation("ES"),
            ProjectChannel.Web,
            tenantId ?? Guid.NewGuid()).Value!;

    private static Project CreateProjectAtContextPhase()
    {
        var project = CreateProject();
        project.StartCapture();
        var capture = CaptureExpedient.Create(
            project.TenantId,
            project.Id).Value!;
        project.CompleteCapture(capture);
        return project;
    }

    private static Project CreateProjectAtScopePhase()
    {
        var project = CreateProjectAtContextPhase();
        var context = CreateProjectContext(project.TenantId, project.Id);
        project.SetContext(context);
        return project;
    }

    private static Project CreateProjectWithWorkItem(out WorkItem workItem)
    {
        var project = CreateProjectAtScopePhase();
        workItem = CreateWorkItem(project.TenantId, project.Id);
        project.AddWorkItem(workItem);
        return project;
    }

    private static ProjectContext CreateProjectContext(
        Guid tenantId,
        ProjectId projectId) =>
        ProjectContext.Create(
            tenantId,
            projectId,
            new TerritorialContext("ES", null, null, null, null),
            new NormativeContext(null, null),
            new LaborContext(null, null, null),
            new EconomicContext(null, null, null)).Value!;

    private static WorkItem CreateWorkItem(
        Guid tenantId,
        ProjectId projectId) =>
        WorkItem.Create(
            tenantId,
            projectId,
            new CanonicalConceptId("FLOOR_INSTALLATION"),
            "Install flooring",
            MeasurementUnit.SquareMeter,
            12m,
            Confidence.High).Value!;
}
