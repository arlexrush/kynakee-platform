namespace Kynakee.Modules.Bots.Domain.ValueObjects;

public readonly record struct BotConversationId(Guid Value)
{
    /// <summary>Creates a new conversation identifier.</summary>
    public static BotConversationId New() => new(Guid.NewGuid());

    /// <summary>Returns the underlying identifier as a string.</summary>
    public override string ToString() => Value.ToString();
}
