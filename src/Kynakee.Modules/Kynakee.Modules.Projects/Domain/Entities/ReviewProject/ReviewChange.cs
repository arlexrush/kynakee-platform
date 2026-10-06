using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.ReviewProject
{
    public sealed record ReviewChange
    {
        private ReviewChange(
            string field,
            string? previousValue,
            string? newValue,
            string? reason)
        {
            Field = field;
            PreviousValue = previousValue;
            NewValue = newValue;
            Reason = reason;
        }

        public string Field { get; }

        public string? PreviousValue { get; }

        public string? NewValue { get; }

        public string? Reason { get; }

        public static Result<ReviewChange> Create(
            string field,
            string? previousValue,
            string? newValue,
            string? reason = null)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return ResultFactory.Failure<ReviewChange>(
                    ApplicationError.Validation(
                        "PROJ_REVIEW_CHANGE_FIELD_REQUIRED",
                        "The changed field is required."));
            }

            return ResultFactory.Success(
                new ReviewChange(
                    field.Trim(),
                    previousValue,
                    newValue,
                    Normalize(reason)));
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
    }
}
