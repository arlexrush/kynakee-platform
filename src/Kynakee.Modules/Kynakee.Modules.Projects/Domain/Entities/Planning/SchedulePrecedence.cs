using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Planning
{
    public sealed record SchedulePrecedence
    {
        private SchedulePrecedence(
            ScheduleActivityId activityId,
            ScheduleActivityId predecessorActivityId,
            PrecedenceType type,
            int lagDays)
        {
            ActivityId = activityId;
            PredecessorActivityId = predecessorActivityId;
            Type = type;
            LagDays = lagDays;
        }

        public ScheduleActivityId ActivityId { get; }

        public ScheduleActivityId PredecessorActivityId { get; }

        public PrecedenceType Type { get; }

        public int LagDays { get; }

        public static Result<SchedulePrecedence> Create(
            ScheduleActivityId activityId,
            ScheduleActivityId predecessorActivityId,
            PrecedenceType type = PrecedenceType.FinishToStart,
            int lagDays = 0)
        {
            if (activityId == default ||
                predecessorActivityId == default)
            {
                return ResultFactory.Failure<SchedulePrecedence>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_PRECEDENCE_ACTIVITY_REQUIRED",
                        "Both precedence activity identifiers are required."));
            }

            if (activityId == predecessorActivityId)
            {
                return ResultFactory.Failure<SchedulePrecedence>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_PRECEDENCE_SELF_REFERENCE",
                        "An activity cannot depend on itself."));
            }

            if (lagDays < 0)
            {
                return ResultFactory.Failure<SchedulePrecedence>(
                    ApplicationError.Validation(
                        "PROJ_SCHEDULE_PRECEDENCE_LAG_INVALID",
                        "Precedence lag cannot be negative."));
            }

            return ResultFactory.Success(
                new SchedulePrecedence(
                    activityId,
                    predecessorActivityId,
                    type,
                    lagDays));
        }
    }
}
