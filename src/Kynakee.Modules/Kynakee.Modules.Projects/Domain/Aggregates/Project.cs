using Kynakee.Modules.Projects.Domain.Entities.DataCapture;
using Kynakee.Modules.Projects.Domain.Entities.DataContext;
using Kynakee.Modules.Projects.Domain.Entities.Events;
using Kynakee.Modules.Projects.Domain.Entities.OfferProject;
using Kynakee.Modules.Projects.Domain.Entities.Planning;
using Kynakee.Modules.Projects.Domain.Entities.ReviewProject;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Aggregates;

public sealed class Project : AggregateRoot<ProjectId>
{
    private readonly List<WorkItem> _workItems = new();
    private readonly List<APUAssignment> _apuAssignments = new();

    private Project()
    {
    }

    private Project(
        ProjectId id,
        Guid tenantId,
        string name,
        ClientInfo client,
        GeoLocation location,
        ProjectChannel channel,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        Name = name;
        Client = client;
        Location = location;
        Channel = channel;
        CurrentPhase = ProjectPhase.Initialization;
        Status = ProjectStatus.Active;
        TotalTokensConsumed = TokenConsumption.Empty;
    }

    public string Name { get; private set; } = string.Empty;

    public ClientInfo Client { get; private set; } = default!;

    public GeoLocation Location { get; private set; } = default!;

    public ProjectChannel Channel { get; private set; }

    public ProjectPhase CurrentPhase { get; private set; }

    public ProjectStatus Status { get; private set; }

    public CaptureExpedient? Capture { get; private set; }

    public ProjectContext? Context { get; private set; }

    public IReadOnlyList<WorkItem> WorkItems =>
        _workItems.AsReadOnly();

    public IReadOnlyList<APUAssignment> APUAssignments =>
    _apuAssignments.Where(assignment => !assignment.IsDeleted).ToList().AsReadOnly();

    public Schedule? Schedule { get; private set; }

    public Valuation? Valuation { get; private set; }

    public Review? Review { get; private set; }

    public Offer? Offer { get; private set; }

    public TokenConsumption TotalTokensConsumed { get; private set; } =
        TokenConsumption.Empty;

    public static Result<Project> Create(
        string name,
        ClientInfo client,
        GeoLocation location,
        ProjectChannel channel,
        Guid tenantId,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            return ResultFactory.Failure<Project>(
                ApplicationError.Validation(
                    "PROJ_TENANT_REQUIRED",
                    "The project tenant is required."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return ResultFactory.Failure<Project>(
                ApplicationError.Validation(
                    "PROJ_NAME_REQUIRED",
                    "The project name is required."));
        }

        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(location);

        var project = new Project(
            ProjectId.New(),
            tenantId,
            name.Trim(),
            client,
            location,
            channel,
            createdBy);

        project.AddDomainEvent(
            new ProjectCreatedEvent(
                project.Id,
                project.TenantId,
                project.Location,
                project.Channel));

        return ResultFactory.Success(project);
    }

    public Result StartCapture(Guid? updatedBy = null)
    {
        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Initialization)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_CAPTURE_INVALID_PHASE",
                    "Capture can only start during project initialization."));
        }

        AdvanceTo(ProjectPhase.Capture, updatedBy);

        return ResultFactory.Ok();
    }

    public Result CompleteCapture(
        CaptureExpedient capture,
        Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(capture);

        var validation = ValidateChild(
            capture.TenantId,
            capture.ProjectId,
            "PROJ_CAPTURE");

        if (!validation.IsSuccess)
        {
            return validation;
        }

        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Capture)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_CAPTURE_INVALID_PHASE",
                    "Capture cannot be completed in the current project phase."));
        }

        Capture = capture;
        AdvanceTo(ProjectPhase.Context, updatedBy);

        return ResultFactory.Ok();
    }

    public Result SetContext(
        ProjectContext context,
        Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var validation = ValidateChild(
            context.TenantId,
            context.ProjectId,
            "PROJ_CONTEXT");

        if (!validation.IsSuccess)
        {
            return validation;
        }

        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Context)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_CONTEXT_INVALID_PHASE",
                    "Context cannot be set in the current project phase."));
        }

        Context = context;
        AdvanceTo(ProjectPhase.Scope, updatedBy);

        return ResultFactory.Ok();
    }

    public Result AddWorkItem(
    WorkItem workItem,
    Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (workItem.TenantId != TenantId)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_WORKITEM_TENANT_MISMATCH",
                    "The work item belongs to another tenant."));
        }

        if (workItem.ProjectId != Id)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_WORKITEM_PROJECT_MISMATCH",
                    "The work item belongs to another project."));
        }

        if (CurrentPhase < ProjectPhase.Scope)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_WORKITEM_INVALID_PHASE",
                    "The scope phase has not been reached."));
        }

        if (_workItems.Any(item => !item.IsDeleted && item.Id == workItem.Id))
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_WORKITEM_ALREADY_EXISTS",
                    "The work item already belongs to the project."));
        }

        _workItems.Add(workItem);

        InvalidateDownstreamResults(
            "A work item was added to the project.",
            updatedBy,
            CurrentPhase >= ProjectPhase.Production &&
            _apuAssignments.Any(assignment => !assignment.IsDeleted)
                ? ProjectPhase.Production
                : ProjectPhase.Scope);

        AddDomainEvent(
            new WorkItemAddedEvent(
                Id,
                TenantId,
                workItem.Id,
                workItem.CanonicalConceptId));

        return ResultFactory.Ok();
    }

    public Result UpdateWorkItemQuantity(
    WorkItemId workItemId,
    decimal newQuantity,
    Guid? updatedBy = null)
    {
        return UpdateWorkItem(
            workItemId,
            workItem => workItem.UpdateQuantity(newQuantity, updatedBy),
            "A work item quantity was modified.",
            updatedBy);
    }

    public Result UpdateWorkItemUnit(
        WorkItemId workItemId,
        MeasurementUnit unit,
        Guid? updatedBy = null)
    {
        return UpdateWorkItem(
        workItemId,
        workItem =>
        {
            if (unit is not null &&
                !workItem.IsDeleted &&
                workItem.Unit != unit &&
                _apuAssignments.Any(assignment =>
                    !assignment.IsDeleted &&
                    assignment.WorkItemId == workItemId))
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_WORKITEM_UNIT_APU_ASSIGNED",
                        "The work item unit cannot change while it has an APU assignment."));
            }

            return workItem.UpdateUnit(unit, updatedBy);
        },
        "A work item unit was modified.",
        updatedBy,
        workItem =>
            unit is not null &&
            !string.IsNullOrWhiteSpace(unit.Code) &&
            workItem.Unit == unit);
    }

    public Result ChangeWorkItemUnitAndReplaceAPU(
    WorkItemId workItemId,
    MeasurementUnit? newUnit,
    APUAssignment replacement,
    Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var activeResult = EnsureProjectIsActive();
        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        var workItem = _workItems.FirstOrDefault(item => !item.IsDeleted && item.Id == workItemId);
        if (workItem is null)
        {
            return ResultFactory.Failure(
                ApplicationError.NotFound(
                    "PROJ_WORKITEM_NOT_FOUND",
                    $"Work item '{workItemId}' was not found in the project."));
        }

        if (newUnit is null || string.IsNullOrWhiteSpace(newUnit.Code))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation(
                    "PROJ_WORKITEM_UNIT_REQUIRED",
                    "The work item measurement unit is required."));
        }

        if (workItem.Unit == newUnit)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_WORKITEM_UNIT_UNCHANGED",
                    "The replacement requires a different work item unit."));
        }

        var previous = _apuAssignments.SingleOrDefault(assignment =>
            !assignment.IsDeleted &&
            assignment.WorkItemId == workItemId);

        if (previous is null)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_WORKITEM_APU_REQUIRED",
                    "The work item has no active APU to replace."));
        }

        if (replacement.IsDeleted ||
            replacement.TenantId != TenantId ||
            replacement.ProjectId != Id ||
            replacement.WorkItemId != workItemId ||
            replacement.OutputUnit != newUnit ||
            _apuAssignments.Any(assignment => assignment.Id == replacement.Id))
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_APU_REPLACEMENT_INVALID",
                    "The replacement APU must be new, active, belong to this work item and use its new unit."));
        }

        // Tras las validaciones, la actualización no puede fallar por una regla
        // de negocio adicional: WorkItem.UpdateUnit ya ha validado la unidad.
        var updateResult = workItem.UpdateUnit(newUnit, updatedBy);
        if (!updateResult.IsSuccess)
        {
            return updateResult;
        }

        previous.Delete(updatedBy);
        _apuAssignments.Add(replacement);

        InvalidateDownstreamResults(
            "A work item unit and its APU were replaced.",
            updatedBy,
            ProjectPhase.Planning);

        AddDomainEvent(new WorkItemUpdatedEvent(Id, TenantId, workItemId));
        return ResultFactory.Ok();
    }

    public Result UpdateWorkItemDescription(
        WorkItemId workItemId,
        string description,
        Guid? updatedBy = null)
    {
        return UpdateWorkItem(
            workItemId,
            workItem => workItem.UpdateDescription(description, updatedBy),
            "A work item description was modified.",
            updatedBy);
    }

    private Result UpdateWorkItem(
        WorkItemId workItemId,
        Func<WorkItem, Result> update,
        string invalidationReason,
        Guid? updatedBy,
        Func<WorkItem, bool>? isUnchanged = null)
    {
        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        var workItem = _workItems.FirstOrDefault(
            item => !item.IsDeleted && item.Id == workItemId);

        if (workItem is null)
        {
            return ResultFactory.Failure(
                ApplicationError.NotFound(
                    "PROJ_WORKITEM_NOT_FOUND",
                    $"Work item '{workItemId}' was not found in the project."));
        }

        if (isUnchanged?.Invoke(workItem) == true)
        {
            return ResultFactory.Ok();
        }

        var updateResult = update(workItem);

        if (!updateResult.IsSuccess)
        {
            return updateResult;
        }

        InvalidateDownstreamResults(
            invalidationReason,
            updatedBy,
            CurrentPhase < ProjectPhase.Production
                ? ProjectPhase.Scope
                : _apuAssignments.Count(assignment => !assignment.IsDeleted) == _workItems.Count(item => !item.IsDeleted)
                    ? ProjectPhase.Planning
                    : ProjectPhase.Production);

        AddDomainEvent(
            new WorkItemUpdatedEvent(
                Id,
                TenantId,
                workItemId));

        return ResultFactory.Ok();
    }

    public Result StartProduction(Guid? updatedBy = null)
    {
        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Scope)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_PRODUCTION_INVALID_PHASE",
                    "Production can only start after the scope phase."));
        }

        var activeWorkItems = _workItems
            .Where(item => !item.IsDeleted)
            .ToArray();

        if (activeWorkItems.Length == 0)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_PRODUCTION_WORKITEMS_REQUIRED",
                    "At least one active work item is required before production."));
        }

        var activeAssignments = _apuAssignments
        .Where(assignment => !assignment.IsDeleted)
        .ToArray();

        var allWorkItemsAssigned =
            activeAssignments.Length == activeWorkItems.Length &&
            activeWorkItems.All(item =>
                activeAssignments.Count(assignment =>
                    assignment.WorkItemId == item.Id &&
                    assignment.OutputUnit == item.Unit) == 1);


        AdvanceTo(
            allWorkItemsAssigned
                ? ProjectPhase.Planning
                : ProjectPhase.Production,
            updatedBy);

        return ResultFactory.Ok();
    }

    public Result AssignAPU(
    APUAssignment assignment,
    Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (assignment.TenantId != TenantId)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_APU_TENANT_MISMATCH",
                    "The APU assignment belongs to another tenant."));
        }

        if (assignment.ProjectId != Id)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_APU_PROJECT_MISMATCH",
                    "The APU assignment belongs to another project."));
        }

        if (CurrentPhase != ProjectPhase.Production)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_APU_INVALID_PHASE",
                    "APUs can only be assigned during the production phase."));
        }


        var workItem = _workItems.FirstOrDefault(item =>
            !item.IsDeleted &&
            item.Id == assignment.WorkItemId);


        if (workItem is null)
        {
            return ResultFactory.Failure(
                ApplicationError.NotFound(
                    "PROJ_WORKITEM_NOT_FOUND",
                    $"Work item '{assignment.WorkItemId}' was not found in the project."));
        }

        if (assignment.OutputUnit != workItem.Unit)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_APU_OUTPUT_UNIT_MISMATCH",
                    "The APU output unit must match the work item unit."));
        }

        if (_apuAssignments.Any(existing =>
                !existing.IsDeleted &&
                existing.WorkItemId == assignment.WorkItemId))
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_WORKITEM_APU_ALREADY_ASSIGNED",
                    $"Work item '{assignment.WorkItemId}' already has an APU."));
        }

        _apuAssignments.Add(assignment);

        if (_apuAssignments.Count(assignment => !assignment.IsDeleted) == _workItems.Count(item => !item.IsDeleted))
        {
            AdvanceTo(ProjectPhase.Planning, updatedBy);
        }
        else
        {
            RegisterUpdate(updatedBy);
        }

        return ResultFactory.Ok();
    }

    public Result AddApuPricingResult(
        APUComponentPricing pricing,
        Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase < ProjectPhase.Production)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_APU_PRICING_INVALID_PHASE",
                    "APU component prices cannot be added before production."));
        }

        var assignment = _apuAssignments.FirstOrDefault(
            candidate => !candidate.IsDeleted && candidate.Components.Any(component => component.Id == pricing.APUComponentId));

        if (assignment is null)
        {
            return ResultFactory.Failure(
                ApplicationError.NotFound(
                    "PROJ_APU_COMPONENT_NOT_FOUND",
                    $"APU component '{pricing.APUComponentId}' does not belong to this project."));
        }

        var pricingResult = assignment.AddPricingResult(
            pricing,
            updatedBy);

        if (!pricingResult.IsSuccess)
        {
            return pricingResult;
        }

        InvalidateDownstreamResults(
        "An APU component price was modified.",
        updatedBy,
        ProjectPhase.Planning);

        return ResultFactory.Ok();
    }

    private Result EnsureProjectIsActive()
    {
        if (Status != ProjectStatus.Active)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_PROJECT_NOT_ACTIVE",
                    "A project that is paused, completed or cancelled cannot be modified."));
        }

        return ResultFactory.Ok();
    }

    public Result SetSchedule(
        Schedule schedule,
        Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Planning)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_SCHEDULE_INVALID_PHASE",
                    "A schedule can only be set during the planning phase."));
        }

        var validation = ValidateChild(
            schedule.TenantId,
            schedule.ProjectId,
            "PROJ_SCHEDULE");

        if (!validation.IsSuccess)
        {
            return validation;
        }


        var activeWorkItemIds = _workItems
            .Where(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToHashSet();

        var scheduledWorkItemIds = schedule.Activities
            .SelectMany(activity => activity.WorkItemIds)
            .ToHashSet();

        if (scheduledWorkItemIds.Any(id => !activeWorkItemIds.Contains(id)))
        {
            return ResultFactory.Failure(
                ApplicationError.NotFound(
                    "PROJ_SCHEDULE_WORKITEM_NOT_FOUND",
                    "The schedule references a work item that is not active in the project."));
        }

        if (activeWorkItemIds.Any(id => !scheduledWorkItemIds.Contains(id)))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation(
                    "PROJ_SCHEDULE_WORKITEMS_INCOMPLETE",
                    "The schedule must include every active project work item."));
        }


        Schedule = schedule;
        AdvanceTo(ProjectPhase.Valuation, updatedBy);

        return ResultFactory.Ok();
    }

    public Result SetValuation(
    Valuation valuation,
    Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(valuation);

        var validation = ValidateChild(
            valuation.TenantId,
            valuation.ProjectId,
            "PROJ_VALUATION");

        if (!validation.IsSuccess)
        {
            return validation;
        }

        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Valuation)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_VALUATION_INVALID_PHASE",
                    "A valuation can only be set during the valuation phase."));
        }

        if (!valuation.IsComplete)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_VALUATION_INCOMPLETE",
                    "The valuation is not complete."));
        }

        var activeWorkItems = _workItems
            .Where(item => !item.IsDeleted)
            .ToArray();

        var activeAssignments = _apuAssignments
            .Where(assignment => !assignment.IsDeleted)
            .ToArray();

        if (activeWorkItems.Length == 0 ||
            activeAssignments.Length != activeWorkItems.Length ||
            activeWorkItems.Any(item =>
                activeAssignments.Count(assignment =>
                    assignment.WorkItemId == item.Id &&
                    assignment.OutputUnit == item.Unit) != 1))
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_VALUATION_APU_STATE_INVALID",
                    "Every active work item must have exactly one compatible active APU."));
        }

        var quantities = activeWorkItems.ToDictionary(
            item => item.Id,
            item => item.Quantity);

        var expectedResult = Valuation.Create(
            TenantId,
            Id,
            activeAssignments,
            quantities);

        if (!expectedResult.IsSuccess)
        {
            return ResultFactory.Failure(expectedResult.Error!);
        }

        var expected = expectedResult.Value!;

        var expectedLines = expected.ValuedWorkItems.ToDictionary(
            item => item.WorkItemId);

        var actualLines = valuation.ValuedWorkItems;

        var linesMatch =
            actualLines.Count == expectedLines.Count &&
            actualLines
                .Select(item => item.WorkItemId)
                .Distinct()
                .Count() == actualLines.Count &&
            actualLines.All(item =>
                expectedLines.TryGetValue(item.WorkItemId, out var expectedItem) &&
                item == expectedItem);

        var expectedComponents = expected.ValuedComponents.ToDictionary(
            component => component.ComponentId);

        var actualComponents = valuation.ValuedComponents;

        var componentsMatch =
            actualComponents.Count == expectedComponents.Count &&
            actualComponents
                .Select(component => component.ComponentId)
                .Distinct()
                .Count() == actualComponents.Count &&
            actualComponents.All(component =>
                expectedComponents.TryGetValue(
                    component.ComponentId,
                    out var expectedComponent) &&
                component == expectedComponent);

        if (!linesMatch ||
            !componentsMatch ||
            valuation.DirectCost != expected.DirectCost ||
            valuation.AuxiliaryCost != expected.AuxiliaryCost ||
            valuation.ConfidenceLevel != expected.ConfidenceLevel)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_VALUATION_STALE_SNAPSHOT",
                    "The valuation does not match the current work items, APU assignments and component prices."));
        }

        Valuation = valuation;
        AdvanceTo(ProjectPhase.Review, updatedBy);

        return ResultFactory.Ok();
    }

    public Result ApplyValuationAdjustments(
        Money indirectCost,
        Money administration,
        Money quality,
        Money safetyHealth,
        Money environment,
        Money contingency,
        Money profit,
        Money vat,
        Guid? updatedBy = null)
    {
        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase is not (ProjectPhase.Review or ProjectPhase.Offer) || Valuation is null)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_VALUATION_ADJUSTMENTS_INVALID_PHASE",
                    "A valuation can only be adjusted after valuation and before the offer is sent."));
        }

        var adjustmentResult = Valuation.ApplyAdjustments(
            indirectCost,
            administration,
            quality,
            safetyHealth,
            environment,
            contingency,
            profit,
            vat,
            updatedBy);

        if (!adjustmentResult.IsSuccess)
        {
            return adjustmentResult;
        }

        Review = null;
        Offer = null;
        CurrentPhase = ProjectPhase.Review;
        RegisterUpdate(updatedBy);

        return ResultFactory.Ok();
    }

    public Result ApproveReview(
        UserId reviewerId,
        IReadOnlyList<ReviewChange> changes,
        Guid? updatedBy = null)
    {
        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Review)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_REVIEW_INVALID_PHASE",
                    "A review can only be approved during the review phase."));
        }

        if (Valuation is null || !Valuation.IsComplete)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_REVIEW_VALUATION_REQUIRED",
                    "A completed valuation is required before review."));
        }

        var reviewResult = Review.Create(
            TenantId,
            Id,
            reviewerId,
            changes,
            null,
            updatedBy);

        if (!reviewResult.IsSuccess)
        {
            return ResultFactory.Failure(
                reviewResult.Error!);
        }

        var review = reviewResult.Value!;

        var approvalResult = review.Approve(updatedBy);

        if (!approvalResult.IsSuccess)
        {
            return ResultFactory.Failure(
                approvalResult.Error!);
        }

        Review = review;

        AdvanceTo(ProjectPhase.Offer, updatedBy);

        AddDomainEvent(
            new ReviewApprovedEvent(
                Id,
                TenantId,
                reviewerId,
                review.Changes.Count));

        return ResultFactory.Ok();
    }

    public Result SetOffer(
        Offer offer,
        Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var validation = ValidateChild(
            offer.TenantId,
            offer.ProjectId,
            "PROJ_OFFER");

        if (!validation.IsSuccess)
        {
            return validation;
        }

        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Offer)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_OFFER_INVALID_PHASE",
                    "An offer can only be set during the offer phase."));
        }

        if (Review is null || !Review.IsApproved)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_OFFER_REVIEW_REQUIRED",
                    "An approved review is required before generating an offer."));
        }

        if (Valuation is null || !Valuation.IsComplete)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_OFFER_VALUATION_REQUIRED",
                    "A completed valuation is required before generating an offer."));
        }

        if (offer.TotalAmount != Valuation.TotalCost)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_OFFER_TOTAL_MISMATCH",
                    "The offer total amount and currency must match the valuation total."));
        }

        Offer = offer;
        RegisterUpdate(updatedBy);

        AddDomainEvent(
            new OfferGeneratedEvent(
                Id,
                TenantId,
                offer.TotalAmount,
                offer.OfferNumber));

        return ResultFactory.Ok();
    }

    public Result SendOffer(
    Uri? pdfUrl = null,
    Guid? updatedBy = null)
    {
        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (CurrentPhase != ProjectPhase.Offer)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_OFFER_INVALID_PHASE",
                    "An offer can only be sent during the offer phase."));
        }

        if (Offer is null)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_OFFER_REQUIRED_FOR_SENDING",
                    "An offer is required before it can be sent."));
        }

        var sendResult = Offer.MarkAsSent(pdfUrl, updatedBy);

        if (!sendResult.IsSuccess)
        {
            return sendResult;
        }

        Status = ProjectStatus.Completed;
        RegisterUpdate(updatedBy);

        return ResultFactory.Ok();
    }

    public Result AcceptOffer(Guid? updatedBy = null) =>
        ChangeSentOfferStatus(offer => offer.Accept(updatedBy), updatedBy);

    public Result RejectOffer(Guid? updatedBy = null) =>
        ChangeSentOfferStatus(offer => offer.Reject(updatedBy), updatedBy);

    public Result ExpireOffer(Guid? updatedBy = null) =>
        ChangeSentOfferStatus(offer => offer.Expire(updatedBy), updatedBy);

    private Result ChangeSentOfferStatus(
        Func<Offer, Result> changeStatus,
        Guid? updatedBy)
    {
        if (Status != ProjectStatus.Completed || CurrentPhase != ProjectPhase.Offer)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_OFFER_RESPONSE_INVALID_STATE",
                    "Only a completed project in the offer phase can register an offer response."));
        }

        if (Offer is null)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_OFFER_REQUIRED_FOR_RESPONSE",
                    "An offer is required before its response can be registered."));
        }

        var result = changeStatus(Offer);

        if (!result.IsSuccess)
        {
            return result;
        }

        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result Pause(
        Guid? updatedBy = null)
    {
        if (Status != ProjectStatus.Active)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_PROJECT_CANNOT_PAUSE",
                    "Only an active project can be paused."));
        }

        Status = ProjectStatus.Paused;
        RegisterUpdate(updatedBy);

        AddDomainEvent(
            new ProjectPausedEvent(
                Id,
                TenantId,
                DateTime.UtcNow,
                Channel));

        return ResultFactory.Ok();
    }

    public Result Resume(Guid? updatedBy = null)
    {
        if (Status != ProjectStatus.Paused)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_PROJECT_NOT_PAUSED",
                    "The project is not paused."));
        }

        Status = ProjectStatus.Active;
        RegisterUpdate(updatedBy);

        return ResultFactory.Ok();
    }

    public Result Cancel(
        string reason,
        Guid? updatedBy = null)
    {
        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    "PROJ_PROJECT_CANNOT_CANCEL",
                    "The project is already in a terminal state."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation(
                    "PROJ_PROJECT_CANCEL_REASON_REQUIRED",
                    "A cancellation reason is required."));
        }

        Status = ProjectStatus.Cancelled;
        RegisterUpdate(updatedBy);

        AddDomainEvent(
            new ProjectCancelledEvent(
                Id,
                TenantId,
                reason.Trim()));

        return ResultFactory.Ok();
    }

    public Result AddTokenConsumption(
        ProjectPhase phase,
        int tokens,
        decimal credits,
        Guid? updatedBy = null)
    {
        var activeResult = EnsureProjectIsActive();

        if (!activeResult.IsSuccess)
        {
            return activeResult;
        }

        if (tokens < 0 || credits < 0)
        {
            return ResultFactory.Failure(
                ApplicationError.Validation(
                    "PROJ_TOKEN_CONSUMPTION_INVALID",
                    "Tokens and credits cannot be negative."));
        }

        TotalTokensConsumed = TotalTokensConsumed.With(
            phase,
            tokens,
            credits);

        RegisterUpdate(updatedBy);

        return ResultFactory.Ok();
    }

    private void InvalidateDownstreamResults(
    string reason,
    Guid? updatedBy,
    ProjectPhase restartPhase = ProjectPhase.Scope)
    {
        var hadDownstreamResults =
            Schedule is not null ||
            Valuation is not null ||
            Review is not null ||
            Offer is not null;

        Schedule = null;
        Valuation = null;
        Review = null;
        Offer = null;

        if (CurrentPhase > restartPhase)
        {
            CurrentPhase = restartPhase;
        }

        RegisterUpdate(updatedBy);

        if (hadDownstreamResults)
        {
            AddDomainEvent(
                new ValuationInvalidatedEvent(
                    Id,
                    TenantId,
                    reason));
        }
    }

    private void AdvanceTo(
        ProjectPhase nextPhase,
        Guid? updatedBy)
    {
        var previousPhase = CurrentPhase;
        CurrentPhase = nextPhase;
        RegisterUpdate(updatedBy);

        AddDomainEvent(
            new PhaseAdvancedEvent(
                Id,
                TenantId,
                previousPhase,
                nextPhase));
    }

    private Result ValidateChild(
        Guid childTenantId,
        ProjectId childProjectId,
        string errorPrefix)
    {
        if (childTenantId != TenantId)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    $"{errorPrefix}_TENANT_MISMATCH",
                    "The entity belongs to another tenant."));
        }

        if (childProjectId != Id)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict(
                    $"{errorPrefix}_PROJECT_MISMATCH",
                    "The entity belongs to another project."));
        }

        return ResultFactory.Ok();
    }
}