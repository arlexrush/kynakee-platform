using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.ReviewProject
{
    public sealed class Review : BaseEntity<ReviewId>
    {
        private readonly List<ReviewChange> _changes = [];

        private Review()
        {
        }

        private Review(
            ReviewId id,
            Guid tenantId,
            ProjectId projectId,
            UserId reviewerId,
            IEnumerable<ReviewChange> changes,
            AIActReviewLog aiActLog,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            ProjectId = projectId;
            ReviewerId = reviewerId;
            IsApproved = false;
            AIActLog = aiActLog;

            _changes.AddRange(changes);
        }

        public ProjectId ProjectId { get; private set; }

        public UserId ReviewerId { get; private set; }

        public bool IsApproved { get; private set; }

        public DateTime? ApprovedAt { get; private set; }

        public IReadOnlyList<ReviewChange> Changes =>
            _changes.AsReadOnly();

        public AIActReviewLog AIActLog { get; private set; } = default!;

        public static Result<Review> Create(
            Guid tenantId,
            ProjectId projectId,
            UserId reviewerId,
            IEnumerable<ReviewChange>? changes = null,
            AIActReviewLog? aiActLog = null,
            Guid? createdBy = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<Review>(
                    ApplicationError.Validation(
                        "PROJ_REVIEW_TENANT_REQUIRED",
                        "The review tenant is required."));
            }

            if (projectId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<Review>(
                    ApplicationError.Validation(
                        "PROJ_REVIEW_PROJECT_REQUIRED",
                        "The review project identifier is required."));
            }

            if (reviewerId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<Review>(
                    ApplicationError.Validation(
                        "PROJ_REVIEW_REVIEWER_REQUIRED",
                        "The reviewer identifier is required."));
            }

            var changeList = changes?.ToArray() ?? [];
            var resolvedAiActLog = aiActLog ?? AIActReviewLog.Empty;

            return ResultFactory.Success(
                new Review(
                    ReviewId.New(),
                    tenantId,
                    projectId,
                    reviewerId,
                    changeList,
                    resolvedAiActLog,
                    createdBy));
        }

        internal Result Approve(
            Guid? updatedBy = null)
        {
            if (IsApproved)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_REVIEW_ALREADY_APPROVED",
                        "The review has already been approved."));
            }

            IsApproved = true;
            ApprovedAt = DateTime.UtcNow;

            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }
    }
}
