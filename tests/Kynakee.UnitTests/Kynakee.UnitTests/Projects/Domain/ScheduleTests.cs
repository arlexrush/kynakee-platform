using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Entities.Planning;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class ScheduleTests
{
    [Fact]
    public void CreateWithEmptyTenantShouldReturnValidationError()
    {
        var activity = CreateActivity();

        var result = Schedule.Create(
            Guid.Empty,
            ProjectId.New(),
            [activity],
            null,
            null,
            1,
            null,
            null,
            null);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_TENANT_REQUIRED");
    }

    [Fact]
    public void CreateWithNegativeDurationShouldReturnValidationError()
    {
        var activity = CreateActivity();

        var result = Schedule.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            [activity],
            null,
            null,
            -1,
            null,
            null,
            null);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_DURATION_INVALID");
    }

    [Fact]
    public void CreateWithEndDateBeforeStartDateShouldReturnValidationError()
    {
        var activity = CreateActivity();

        var result = Schedule.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            [activity],
            null,
            null,
            1,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 1, 31),
            null);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_DATE_RANGE_INVALID");
    }

    [Fact]
    public void CreateWithoutActivitiesShouldReturnValidationError()
    {
        var result = Schedule.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            [],
            null,
            null,
            0,
            null,
            null,
            null);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_ACTIVITIES_REQUIRED");
    }

    [Fact]
    public void CreateWithDuplicateActivitiesShouldReturnValidationError()
    {
        var activityId = Guid.NewGuid();
        var first = CreateActivity(activityId);
        var second = CreateActivity(activityId);

        var result = CreateSchedule([first, second]);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_ACTIVITIES_DUPLICATED");
    }

    [Fact]
    public void CreateWithPrecedenceToMissingActivityShouldReturnValidationError()
    {
        var activity = CreateActivity();
        var precedence = SchedulePrecedence.Create(
            activity.Id,
            ScheduleActivityId.New()).Value!;

        var result = CreateSchedule([activity], precedences: [precedence]);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_PRECEDENCE_ACTIVITY_NOT_FOUND");
    }

    [Fact]
    public void CreateWithCriticalPathToMissingActivityShouldReturnValidationError()
    {
        var activity = CreateActivity();

        var result = CreateSchedule(
            [activity],
            criticalPath: [ScheduleActivityId.New()]);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_CRITICAL_PATH_ACTIVITY_NOT_FOUND");
    }

    [Fact]
    public void CreateWithValidDataShouldRetainActivitiesAndDates()
    {
        var activity = CreateActivity();
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 1, 10);

        var result = Schedule.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            [activity],
            null,
            [activity.Id],
            10,
            startDate,
            endDate,
            null);

        result.Value!.Activities.Should().ContainSingle().Which.Should().BeSameAs(activity);
    }

    [Fact]
    public void CreateValidScheduleShouldRetainItsDateRange()
    {
        var activity = CreateActivity();
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 1, 10);

        var result = Schedule.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            [activity],
            null,
            null,
            10,
            startDate,
            endDate,
            null);

        result.Value!.EndDate.Should().Be(endDate);
    }

    [Fact]
    public void CreateActivityWithBlankNameShouldReturnValidationError()
    {
        var result = ScheduleActivity.Create(
            " ",
            1,
            [WorkItemId.New()]);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_ACTIVITY_NAME_REQUIRED");
    }

    [Fact]
    public void CreateActivityWithNonPositiveDurationShouldReturnValidationError()
    {
        var result = ScheduleActivity.Create(
            "Install flooring",
            0,
            [WorkItemId.New()]);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_ACTIVITY_DURATION_INVALID");
    }

    [Fact]
    public void CreateActivityWithoutWorkItemsShouldReturnValidationError()
    {
        var result = ScheduleActivity.Create(
            "Install flooring",
            1,
            []);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_ACTIVITY_WORKITEMS_REQUIRED");
    }

    [Fact]
    public void CreateActivityShouldRemoveDuplicateWorkItemIds()
    {
        var workItemId = WorkItemId.New();

        var result = ScheduleActivity.Create(
            "Install flooring",
            1,
            [workItemId, workItemId]);

        result.Value!.WorkItemIds.Should().ContainSingle();
    }

    [Fact]
    public void CreatePrecedenceWithNegativeLagShouldReturnValidationError()
    {
        var result = SchedulePrecedence.Create(
            ScheduleActivityId.New(),
            ScheduleActivityId.New(),
            lagDays: -1);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_PRECEDENCE_LAG_INVALID");
    }

    [Fact]
    public void CreatePrecedenceForSameActivityShouldReturnValidationError()
    {
        var activityId = ScheduleActivityId.New();

        var result = SchedulePrecedence.Create(activityId, activityId);

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_PRECEDENCE_SELF_REFERENCE");
    }

    [Fact]
    public void CreateMilestoneWithBlankNameShouldReturnValidationError()
    {
        var result = ScheduleMilestone.Create(" ", new DateOnly(2026, 1, 1));

        result.Error!.Code.Should().Be("PROJ_SCHEDULE_MILESTONE_NAME_REQUIRED");
    }

    [Fact]
    public void CreateMilestoneShouldTrimItsName()
    {
        var result = ScheduleMilestone.Create(
            "  Foundation complete  ",
            new DateOnly(2026, 1, 1));

        result.Value!.Name.Should().Be("Foundation complete");
    }

    private static Result<Schedule> CreateSchedule(
        IEnumerable<ScheduleActivity> activities,
        IEnumerable<SchedulePrecedence>? precedences = null,
        IEnumerable<ScheduleActivityId>? criticalPath = null) =>
        Schedule.Create(
            Guid.NewGuid(),
            ProjectId.New(),
            activities,
            precedences,
            criticalPath,
            1,
            null,
            null,
            null);

    private static ScheduleActivity CreateActivity(Guid? id = null) =>
        ScheduleActivity.Create(
            "Install flooring",
            1,
            [WorkItemId.New()],
            id).Value!;
}
