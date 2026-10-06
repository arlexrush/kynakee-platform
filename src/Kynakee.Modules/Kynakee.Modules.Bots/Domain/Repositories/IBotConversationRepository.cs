using Kynakee.Modules.Bots.Domain.Aggregates;
using Kynakee.Modules.Bots.Domain.Entities;
using Kynakee.Modules.Bots.Domain.ValueObjects;

namespace Kynakee.Modules.Bots.Domain.Repositories;

public interface IBotConversationRepository
{
    Task<BotConversation?> GetByIdForUpdateAsync(
        BotConversationId conversationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        BotConversation conversation,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BotMessage>> GetMessagesPageAsync(
        BotConversationId conversationId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken);
}