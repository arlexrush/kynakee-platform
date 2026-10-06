using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Planning
{
    public sealed record ScheduleActivity
    {
        private ScheduleActivity(
            ScheduleActivityId id,
            string name,
            int durationDays,
            IReadOnlyList<WorkItemId> workItemIds)
        {
            Id = id;
            Name = name;
            DurationDays = durationDays;
            WorkItemIds = workItemIds;
        }

        public ScheduleActivityId Id { get; }

        public string Name { get; }

        public int DurationDays { get; }

        public IReadOnlyList<WorkItemId> WorkItemIds { get; }


        public static Result<ScheduleActivity> Create(
            string name,
            int durationDays,
            IEnumerable<WorkItemId> workItemIds,
            Guid? id = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ResultFactory.Failure<ScheduleActivity>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_ACTIVITY_NAME_REQUIRED",
                        "The schedule activity name is required."));
            }

            if (durationDays <= 0)
            {
                return ResultFactory.Failure<ScheduleActivity>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_ACTIVITY_DURATION_INVALID",
                        "The activity duration must be greater than zero."));
            }

            ArgumentNullException.ThrowIfNull(workItemIds);

            var workItemIdList = workItemIds
                .Distinct()
                .ToArray();

            if (workItemIdList.Length == 0)
            {
                return ResultFactory.Failure<ScheduleActivity>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_ACTIVITY_WORKITEMS_REQUIRED",
                        "An activity must reference at least one work item."));
            }

            if (id.HasValue && id.Value == Guid.Empty)
            {
                return ResultFactory.Failure<ScheduleActivity>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_ACTIVITY_ID_INVALID",
                        "The schedule activity identifier is invalid."));
            }

            var activityId = id is null
                ? ScheduleActivityId.New()
                : new ScheduleActivityId(id.Value);

            if (activityId == default)
            {
                return ResultFactory.Failure<ScheduleActivity>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_ACTIVITY_ID_INVALID",
                        "The schedule activity identifier is invalid."));
            }

            return ResultFactory.Success(
                new ScheduleActivity(
                    activityId,
                    name.Trim(),
                    durationDays,
                    workItemIdList));
        }
    }
}
