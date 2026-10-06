using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Planning
{
    public sealed record ScheduleMilestone
    {
        private ScheduleMilestone(
            string name,
            DateOnly date)
        {
            Name = name;
            Date = date;
        }

        public string Name { get; }

        public DateOnly Date { get; }

        public static Result<ScheduleMilestone> Create(
            string name,
            DateOnly date)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ResultFactory.Failure<ScheduleMilestone>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_MILESTONE_NAME_REQUIRED",
                        "The milestone name is required."));
            }

            return ResultFactory.Success(
                new ScheduleMilestone(name.Trim(), date));
        }
    }
}
