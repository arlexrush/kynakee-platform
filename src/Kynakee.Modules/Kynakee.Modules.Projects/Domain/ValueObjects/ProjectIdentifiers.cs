namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public readonly record struct ProjectId(Guid Value)
    {
        public static ProjectId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct WorkItemId(Guid Value)
    {
        public static WorkItemId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct APUComponentId(Guid Value)
    {
        public static APUComponentId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct APUComponentPricingId(Guid Value)
    {
        public static APUComponentPricingId New() =>
            new(Guid.NewGuid());

        public override string ToString() =>
            Value.ToString();
    }

    public readonly record struct APUAssignmentId(Guid Value)
    {
        public static APUAssignmentId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct ScheduleActivityId(Guid Value)
    {
        public static ScheduleActivityId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct ValuationId(Guid Value)
    {
        public static ValuationId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct CaptureExpedientId(Guid Value)
    {
        public static CaptureExpedientId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct ProjectContextId(Guid Value)
    {
        public static ProjectContextId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct ScheduleId(Guid Value)
    {
        public static ScheduleId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct ReviewId(Guid Value)
    {
        public static ReviewId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct OfferId(Guid Value)
    {
        public static OfferId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct APUTemplateId(Guid Value)
    {
        public static APUTemplateId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }

    public readonly record struct UserId(Guid Value)
    {
        public static UserId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }
}
