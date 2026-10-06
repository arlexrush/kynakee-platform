using Kynakee.Modules.Bots.Domain.Aggregates;
using Kynakee.Modules.Bots.Domain.Entities;
using Kynakee.Modules.Bots.Domain.Repositories;
using Kynakee.Modules.Bots.Domain.ValueObjects;
using Kynakee.Modules.Bots.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Bots.Infrastructure.Repositories;

public sealed class EfBotConversationRepository : IBotConversationRepository
{
    private readonly BotsDbContext _dbContext;

    public EfBotConversationRepository(BotsDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<BotConversation?> GetByIdForUpdateAsync(
        BotConversationId conversationId,
        CancellationToken cancellationToken) =>
        _dbContext.Conversations
            .AsTracking()
            .SingleOrDefaultAsync(
                conversation => conversation.Id == conversationId,
                cancellationToken);

    public async Task AddAsync(
        BotConversation conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        await _dbContext.Conversations
            .AddAsync(conversation, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BotMessage>> GetMessagesPageAsync(
        BotConversationId conversationId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        return await _dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}