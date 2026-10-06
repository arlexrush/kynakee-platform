namespace Kynakee.Modules.Bots.Domain.ValueObjects;

public readonly record struct BotMessageId(Guid Value)
{
    /// <summary>Creates a new message identifier.</summary>
    public static BotMessageId New() => new(Guid.NewGuid());

    /// <summary>Returns the underlying identifier as a string.</summary>
    public override string ToString() => Value.ToString();
}
