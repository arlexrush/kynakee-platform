using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped
{
    public sealed class WorkItem : BaseEntity<WorkItemId>
    {
        private WorkItem()
        {
        }

        private WorkItem(
            WorkItemId id,
            Guid tenantId,
            ProjectId projectId,
            CanonicalConceptId canonicalConceptId,
            string description,
            MeasurementUnit unit,
            decimal quantity,
            string? location,
            string? observations,
            Confidence confidence,
            WorkItemAIStatus aiStatus,
            int sortOrder,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            ProjectId = projectId;
            CanonicalConceptId = canonicalConceptId;
            Description = description;
            Unit = unit;
            Quantity = quantity;
            Location = location;
            Observations = observations;
            Confidence = confidence;
            AIStatus = aiStatus;
            SortOrder = sortOrder;
        }

        public ProjectId ProjectId { get; private set; }

        public CanonicalConceptId CanonicalConceptId { get; private set; } =
            default!;

        public string Description { get; private set; } =
            string.Empty;

        public MeasurementUnit Unit { get; private set; } =
            default!;

        public decimal Quantity { get; private set; }

        public string? Location { get; private set; }

        public string? Observations { get; private set; }

        public Confidence Confidence { get; private set; } =
            default!;

        public WorkItemAIStatus AIStatus { get; private set; }

        public int SortOrder { get; private set; }

        public static Result<WorkItem> Create(
            Guid tenantId,
            ProjectId projectId,
            CanonicalConceptId canonicalConceptId,
            string description,
            MeasurementUnit unit,
            decimal quantity,
            Confidence confidence,
            string? location = null,
            string? observations = null,
            WorkItemAIStatus aiStatus = WorkItemAIStatus.GeneratedByAI,
            int sortOrder = 0,
            Guid? createdBy = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<WorkItem>(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_TENANT_REQUIRED",
                        "The work item tenant is required."));
            }

            if (projectId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<WorkItem>(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_PROJECT_REQUIRED",
                        "The work item project identifier is required."));
            }

            if (canonicalConceptId is null ||
                string.IsNullOrWhiteSpace(canonicalConceptId.Value))
            {
                return ResultFactory.Failure<WorkItem>(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_CANONICAL_CONCEPT_REQUIRED",
                        "The canonical concept identifier is required."));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return ResultFactory.Failure<WorkItem>(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_DESCRIPTION_REQUIRED",
                        "The work item description is required."));
            }

            if (unit is null ||
                string.IsNullOrWhiteSpace(unit.Code))
            {
                return ResultFactory.Failure<WorkItem>(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_UNIT_REQUIRED",
                        "The work item measurement unit is required."));
            }

            if (quantity <= 0)
            {
                return ResultFactory.Failure<WorkItem>(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_QUANTITY_INVALID",
                        "The work item quantity must be greater than zero."));
            }

            if (confidence is null)
            {
                return ResultFactory.Failure<WorkItem>(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_CONFIDENCE_REQUIRED",
                        "The work item confidence is required."));
            }

            if (sortOrder < 0)
            {
                return ResultFactory.Failure<WorkItem>(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_SORT_ORDER_INVALID",
                        "The work item sort order cannot be negative."));
            }

            return ResultFactory.Success(
                new WorkItem(
                    WorkItemId.New(),
                    tenantId,
                    projectId,
                    canonicalConceptId,
                    description.Trim(),
                    unit,
                    quantity,
                    Normalize(location),
                    Normalize(observations),
                    confidence,
                    aiStatus,
                    sortOrder,
                    createdBy));
        }

        internal Result UpdateQuantity(
            decimal newQuantity,
            Guid? updatedBy = null)
        {
            if (newQuantity <= 0)
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_QUANTITY_INVALID",
                        "The work item quantity must be greater than zero."));
            }

            Quantity = newQuantity;
            AIStatus = WorkItemAIStatus.ModifiedByHuman;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        internal Result UpdateUnit(
            MeasurementUnit? unit,
            Guid? updatedBy = null)
        {
            if (unit is null ||
                string.IsNullOrWhiteSpace(unit.Code))
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_UNIT_REQUIRED",
                        "The work item measurement unit is required."));
            }

            Unit = unit;
            AIStatus = WorkItemAIStatus.ModifiedByHuman;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        internal Result UpdateDescription(
            string description,
            Guid? updatedBy = null)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_WORKITEM_DESCRIPTION_REQUIRED",
                        "The work item description is required."));
            }

            Description = description.Trim();
            AIStatus = WorkItemAIStatus.ModifiedByHuman;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        private static string? Normalize(
            string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
    }
}
