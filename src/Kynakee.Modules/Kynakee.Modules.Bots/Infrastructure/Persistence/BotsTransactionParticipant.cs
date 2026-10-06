using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Bots.Infrastructure.Persistence;

public sealed class BotsTransactionParticipant : IModuleTransactionParticipant
{
    private readonly BotsDbContext _dbContext;

    public BotsTransactionParticipant(BotsDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public bool CanHandle(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return requestType.Assembly == typeof(BotsTransactionParticipant).Assembly;
    }

    public async Task<IModuleTransactionScope> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        return new BotsModuleTransactionScope(_dbContext, transaction);
    }

    public IReadOnlyCollection<IDomainEvent> CollectDomainEvents() =>
        _dbContext.ChangeTracker
            .Entries<IDomainEventSource>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToArray();

    public void ClearDomainEvents(IReadOnlyCollection<IDomainEvent> domainEvents)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var entry in _dbContext.ChangeTracker
                     .Entries<IDomainEventSource>())
        {
            entry.Entity.ClearDomainEvents(domainEvents);
        }
    }
}
