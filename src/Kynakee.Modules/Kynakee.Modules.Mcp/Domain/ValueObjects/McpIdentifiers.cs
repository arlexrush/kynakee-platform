namespace Kynakee.Modules.Mcp.Domain.ValueObjects;

public readonly record struct McpProviderId(Guid Value)
{
    public static McpProviderId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct McpServerId(Guid Value)
{
    public static McpServerId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct McpQueryLogId(Guid Value)
{
    public static McpQueryLogId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
