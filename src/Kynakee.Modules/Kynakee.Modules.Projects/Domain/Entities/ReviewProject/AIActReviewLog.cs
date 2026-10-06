using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.ReviewProject
{
    public sealed record AIActReviewLog
    {
        private AIActReviewLog(
            Guid? supervisingHumanId,
            DateTime reviewedAt,
            int itemsReviewed,
            int itemsModified,
            bool article14Confirmed)
        {
            SupervisingHumanId = supervisingHumanId;
            ReviewedAt = reviewedAt;
            ItemsReviewed = itemsReviewed;
            ItemsModified = itemsModified;
            Article14Confirmed = article14Confirmed;
        }

        public Guid? SupervisingHumanId { get; }

        public DateTime ReviewedAt { get; }

        public int ItemsReviewed { get; }

        public int ItemsModified { get; }

        public bool Article14Confirmed { get; }

        public static AIActReviewLog Empty =>
            new(
                null,
                DateTime.UtcNow,
                0,
                0,
                false);

        public static Result<AIActReviewLog> Create(
            Guid? supervisingHumanId,
            int itemsReviewed,
            int itemsModified,
            bool article14Confirmed,
            DateTime? reviewedAt = null)
        {
            if (itemsReviewed < 0)
            {
                return ResultFactory.Failure<AIActReviewLog>(
                    ApplicationError.Validation(
                        "PROJ_REVIEW_ITEMS_REVIEWED_INVALID",
                        "The number of reviewed items cannot be negative."));
            }

            if (itemsModified < 0 || itemsModified > itemsReviewed)
            {
                return ResultFactory.Failure<AIActReviewLog>(
                    ApplicationError.Validation(
                        "PROJ_REVIEW_ITEMS_MODIFIED_INVALID",
                        "The number of modified items must be between zero and the reviewed item count."));
            }

            if (article14Confirmed && supervisingHumanId is null)
            {
                return ResultFactory.Failure<AIActReviewLog>(
                    ApplicationError.Validation(
                        "PROJ_REVIEW_SUPERVISOR_REQUIRED",
                        "A supervising human is required when Article 14 compliance is confirmed."));
            }

            return ResultFactory.Success(
                new AIActReviewLog(
                    supervisingHumanId,
                    reviewedAt ?? DateTime.UtcNow,
                    itemsReviewed,
                    itemsModified,
                    article14Confirmed));
        }
    }
}
