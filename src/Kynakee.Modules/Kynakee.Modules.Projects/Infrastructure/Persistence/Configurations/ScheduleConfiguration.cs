using System.Text.Json;
using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.Planning;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

public sealed class ScheduleConfiguration :
    IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("schedules");

        builder.HasKey(schedule => schedule.Id);

        builder.HasAlternateKey(schedule => new
        {
            schedule.Id,
            schedule.ProjectId,
            schedule.TenantId
        });

        builder.Property(schedule => schedule.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new ScheduleId(value))
            .ValueGeneratedNever();

        builder.Property(schedule => schedule.ProjectId)
            .HasColumnName("project_id")
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(schedule => schedule.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(schedule => schedule.TotalDurationDays)
            .HasColumnName("total_duration_days")
            .IsRequired();

        builder.Property(schedule => schedule.StartDate)
            .HasColumnName("start_date");

        builder.Property(schedule => schedule.EndDate)
            .HasColumnName("end_date");

        builder.Property(schedule => schedule.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(schedule => schedule.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(schedule => schedule.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(schedule => schedule.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(schedule => schedule.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(schedule => schedule.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(schedule => schedule.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(schedule => new
            {
                schedule.ProjectId,
                schedule.TenantId
            })
            .HasPrincipalKey(project => new
            {
                project.Id,
                project.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(
            schedule => schedule.Activities,
            activity =>
            {
                ConfigureOwnedCollection(activity, "schedule_activities");

                activity.Property(value => value.Id)
                    .HasColumnName("id")
                    .HasConversion(
                        id => id.Value,
                        value => new ScheduleActivityId(value))
                    .ValueGeneratedNever();

                activity.HasKey(
                    "ScheduleId",
                    "TenantId",
                    nameof(ScheduleActivity.Id));

                activity.Property(value => value.Name)
                    .HasColumnName("name")
                    .HasMaxLength(300)
                    .IsRequired();

                activity.Property(value => value.DurationDays)
                    .HasColumnName("duration_days")
                    .IsRequired();

                var workItemIds = activity
                    .Property(value => value.WorkItemIds)
                    .HasColumnName("work_item_ids")
                    .HasColumnType("jsonb")
                    .HasConversion(
                        ids => SerializeWorkItemIds(ids),
                        json => DeserializeWorkItemIds(json))
                    .IsRequired();

                workItemIds.Metadata.SetValueComparer(
                    new ValueComparer<IReadOnlyList<WorkItemId>>(
                        (left, right) => left == null ? right == null : right != null &&
                        SerializeWorkItemIds(left) == SerializeWorkItemIds(right),
                        value => value == null ? 0 : SerializeWorkItemIds(value).GetHashCode(),
                        value => value == null ? null! : value.ToArray()));
            });

        builder.Navigation(schedule => schedule.Activities)
            .HasField("_activities")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(
            schedule => schedule.Precedences,
            precedence =>
            {
                ConfigureOwnedCollection(
                    precedence,
                    "schedule_precedences");

                precedence.Property<Guid>("EntryId")
                    .HasColumnName("entry_id")
                    .ValueGeneratedOnAdd();

                precedence.HasKey(
                    "ScheduleId",
                    "TenantId",
                    "EntryId");

                precedence.Property(value => value.ActivityId)
                    .HasColumnName("activity_id")
                    .HasConversion(
                        id => id.Value,
                        value => new ScheduleActivityId(value))
                    .IsRequired();

                precedence.Property(value => value.PredecessorActivityId)
                    .HasColumnName("predecessor_activity_id")
                    .HasConversion(
                        id => id.Value,
                        value => new ScheduleActivityId(value))
                    .IsRequired();

                precedence.Property(value => value.Type)
                    .HasColumnName("type")
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .IsRequired();

                precedence.Property(value => value.LagDays)
                    .HasColumnName("lag_days")
                    .IsRequired();
            });

        builder.Navigation(schedule => schedule.Precedences)
            .HasField("_precedences")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(
            schedule => schedule.Milestones,
            milestone =>
            {
                ConfigureOwnedCollection(
                    milestone,
                    "schedule_milestones");

                milestone.Property<Guid>("EntryId")
                    .HasColumnName("entry_id")
                    .ValueGeneratedOnAdd();

                milestone.HasKey(
                    "ScheduleId",
                    "TenantId",
                    "EntryId");

                milestone.Property(value => value.Name)
                    .HasColumnName("name")
                    .HasMaxLength(300)
                    .IsRequired();

                milestone.Property(value => value.Date)
                    .HasColumnName("date")
                    .IsRequired();
            });

        builder.Navigation(schedule => schedule.Milestones)
            .HasField("_milestones")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(schedule => schedule.CriticalPath);

        var criticalPath = builder
            .Property<List<ScheduleActivityId>>("_criticalPath")
            .HasColumnName("critical_path")
            .HasColumnType("jsonb")
            .HasConversion(
                ids => SerializeActivityIds(ids),
                json => DeserializeActivityIds(json))
            .IsRequired();

        criticalPath.Metadata.SetValueComparer(
            new ValueComparer<List<ScheduleActivityId>>(
                (left, right) => left == null ? right == null : right != null &&
                    SerializeActivityIds(left) == SerializeActivityIds(right),
                value => value == null ? 0 : SerializeActivityIds(value).GetHashCode(),
                value => value.ToList()));
    }

    private static void ConfigureOwnedCollection<TItem>(
        OwnedNavigationBuilder<Schedule, TItem> builder,
        string tableName)
        where TItem : class
    {
        builder.ToTable(tableName);

        builder.Property<ScheduleId>("ScheduleId")
            .HasColumnName("schedule_id")
            .HasConversion(
                id => id.Value,
                value => new ScheduleId(value));

        builder.Property<Guid>("TenantId")
            .HasColumnName("tenant_id");

        builder.WithOwner()
            .HasForeignKey("ScheduleId", "TenantId")
            .HasPrincipalKey(
                nameof(Schedule.Id),
                nameof(Schedule.TenantId));
    }

    private static string SerializeWorkItemIds(
        IReadOnlyList<WorkItemId> ids) =>
        JsonSerializer.Serialize(ids.Select(id => id.Value));

    private static WorkItemId[] DeserializeWorkItemIds(
        string json) =>
        (JsonSerializer.Deserialize<Guid[]>(json) ?? [])
            .Select(value => new WorkItemId(value))
            .ToArray();

    private static string SerializeActivityIds(
        List<ScheduleActivityId> ids) =>
        JsonSerializer.Serialize(ids.Select(id => id.Value));

    private static List<ScheduleActivityId> DeserializeActivityIds(
        string json) =>
        (JsonSerializer.Deserialize<Guid[]>(json) ?? [])
            .Select(value => new ScheduleActivityId(value))
            .ToList();
}
