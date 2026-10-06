using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kynakee.Modules.Ai.Infrastructure.Persistence
{
    public class AiTransactionParticipant : IModuleTransactionParticipant
    {
        private readonly AiDbContext _dbContext;
        public AiTransactionParticipant(AiDbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            _dbContext = dbContext;
        }

        public bool CanHandle(Type requestType)
        {
            ArgumentNullException.ThrowIfNull(requestType);
            return requestType.Assembly == typeof(AiTransactionParticipant).Assembly;
        }

        public async Task<IModuleTransactionScope> BeginTransactionAsync(
            CancellationToken cancellationToken)
        {
            var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            return new AiModuleTransactionScope(_dbContext, transaction);
        }

        public IReadOnlyCollection<IDomainEvent> CollectDomainEvents()
        {
            return _dbContext.ChangeTracker
                .Entries<IDomainEventSource>()
                .SelectMany(entry => entry.Entity.DomainEvents)
                .ToArray();
        }

        public void ClearDomainEvents(
            IReadOnlyCollection<IDomainEvent> domainEvents)
        {
            ArgumentNullException.ThrowIfNull(domainEvents);

            foreach (var entry in _dbContext.ChangeTracker
                         .Entries<IDomainEventSource>())
            {
                entry.Entity.ClearDomainEvents(domainEvents);
            }
        }

    }
}
