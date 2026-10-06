using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.Planning
{
    public sealed class Schedule : BaseEntity<ScheduleId>
    {
        private readonly List<ScheduleActivity> _activities = [];
        private readonly List<SchedulePrecedence> _precedences = [];
        private readonly List<ScheduleActivityId> _criticalPath = [];
        private readonly List<ScheduleMilestone> _milestones = [];

        private Schedule()
        {
        }

        private Schedule(
            ScheduleId id,
            Guid tenantId,
            ProjectId projectId,
            IEnumerable<ScheduleActivity> activities,
            IEnumerable<SchedulePrecedence> precedences,
            IEnumerable<ScheduleActivityId> criticalPath,
            int totalDurationDays,
            DateOnly? startDate,
            DateOnly? endDate,
            IEnumerable<ScheduleMilestone> milestones,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            ProjectId = projectId;
            TotalDurationDays = totalDurationDays;
            StartDate = startDate;
            EndDate = endDate;

            _activities.AddRange(activities);
            _precedences.AddRange(precedences);
            _criticalPath.AddRange(criticalPath);
            _milestones.AddRange(milestones);
        }

        public ProjectId ProjectId { get; private set; }

        public IReadOnlyList<ScheduleActivity> Activities =>
            _activities.AsReadOnly();

        public IReadOnlyList<SchedulePrecedence> Precedences =>
            _precedences.AsReadOnly();

        public IReadOnlyList<ScheduleActivityId> CriticalPath =>
            _criticalPath.AsReadOnly();

        public int TotalDurationDays { get; private set; }

        public DateOnly? StartDate { get; private set; }

        public DateOnly? EndDate { get; private set; }

        public IReadOnlyList<ScheduleMilestone> Milestones =>
            _milestones.AsReadOnly();

        public static Result<Schedule> Create(
            Guid tenantId,
            ProjectId projectId,
            IEnumerable<ScheduleActivity> activities,
            IEnumerable<SchedulePrecedence>? precedences,
            IEnumerable<ScheduleActivityId>? criticalPath,
            int totalDurationDays,
            DateOnly? startDate,
            DateOnly? endDate,
            IEnumerable<ScheduleMilestone>? milestones,
            Guid? createdBy = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<Schedule>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_TENANT_REQUIRED",
                        "The schedule tenant is required."));
            }

            if (projectId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<Schedule>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_PROJECT_REQUIRED",
                        "The schedule project identifier is required."));
            }

            if (totalDurationDays < 0)
            {
                return ResultFactory.Failure<Schedule>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_DURATION_INVALID",
                        "The schedule duration cannot be negative."));
            }

            if (startDate.HasValue &&
                endDate.HasValue &&
                endDate.Value < startDate.Value)
            {
                return ResultFactory.Failure<Schedule>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_DATE_RANGE_INVALID",
                        "The schedule end date cannot precede its start date."));
            }

            ArgumentNullException.ThrowIfNull(activities);

            var activityList = activities.ToArray();
            var precedenceList = precedences?.ToArray() ?? [];
            var criticalPathList = criticalPath?.ToArray() ?? [];
            var milestoneList = milestones?.ToArray() ?? [];

            if (activityList.Length == 0)
            {
                return ResultFactory.Failure<Schedule>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_ACTIVITIES_REQUIRED",
                        "A schedule must contain at least one activity."));
            }

            if (activityList
                .GroupBy(activity => activity.Id)
                .Any(group => group.Count() > 1))
            {
                return ResultFactory.Failure<Schedule>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_ACTIVITIES_DUPLICATED",
                        "A schedule cannot contain duplicated activities."));
            }

            var activityIds = activityList
                .Select(activity => activity.Id)
                .ToHashSet();

            foreach (var precedence in precedenceList)
            {
                if (!activityIds.Contains(precedence.ActivityId) ||
                    !activityIds.Contains(precedence.PredecessorActivityId))
                {
                    return ResultFactory.Failure<Schedule>(
                        ApplicationError.Validation(
                            "PROJ_SCHEDULE_PRECEDENCE_ACTIVITY_NOT_FOUND",
                            "Every precedence must reference activities from the schedule."));
                }

                if (precedence.ActivityId == precedence.PredecessorActivityId)
                {
                    return ResultFactory.Failure<Schedule>(
                        ApplicationError.Validation(
                            "PROJ_SCHEDULE_PRECEDENCE_SELF_REFERENCE",
                            "An activity cannot depend on itself."));
                }
            }

            foreach (var criticalPathItem in criticalPathList)
            {
                if (!activityList.Any(activity =>
                        activity.Id == criticalPathItem))
                {
                    return ResultFactory.Failure<Schedule>(
                        ApplicationError.Validation(
                            "PROJ_SCHEDULE_CRITICAL_PATH_ACTIVITY_NOT_FOUND",
                            $"Activity '{criticalPathItem}' is not referenced by the schedule."));
                }
            }

            return ResultFactory.Success(
                new Schedule(
                    ScheduleId.New(),
                    tenantId,
                    projectId,
                    activityList,
                    precedenceList,
                    criticalPathList,
                    totalDurationDays,
                    startDate,
                    endDate,
                    milestoneList,
                    createdBy));
        }

        internal Result ReplaceCriticalPath(
            IEnumerable<ScheduleActivityId> criticalPath,
            Guid? updatedBy = null)
        {
            ArgumentNullException.ThrowIfNull(criticalPath);

            var criticalPathList = criticalPath.Distinct().ToArray();

            var activityIds = _activities.Select(activity => activity.Id).ToHashSet();

            if (criticalPathList.Any(activityId => !activityIds.Contains(activityId)))
            {
                return ResultFactory.Failure(
                    ApplicationError.NotFound(
                        "PROJ_SCHEDULE_CRITICAL_PATH_ACTIVITY_NOT_FOUND",
                        "Every critical path activity must belong to the schedule."));
            }

            _criticalPath.Clear();
            _criticalPath.AddRange(criticalPathList);

            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }
    }
}
