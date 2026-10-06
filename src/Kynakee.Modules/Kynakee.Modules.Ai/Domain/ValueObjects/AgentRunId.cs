namespace Kynakee.Modules.Ai.Domain.ValueObjects;

public readonly record struct AgentRunId(Guid Value)
{
    public static AgentRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
