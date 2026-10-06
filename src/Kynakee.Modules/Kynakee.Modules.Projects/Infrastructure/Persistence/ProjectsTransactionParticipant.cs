using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence
{
    public class ProjectsTransactionParticipant: IModuleTransactionParticipant
    {
        private readonly ProjectsDbContext _dbContext;

        public ProjectsTransactionParticipant(ProjectsDbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            _dbContext = dbContext;
        }

        public bool CanHandle(Type requestType)
        {
            ArgumentNullException.ThrowIfNull(requestType);
            return requestType.Assembly == typeof(Project).Assembly;
        }

        public async Task<IModuleTransactionScope> BeginTransactionAsync(
            CancellationToken cancellationToken)
        {
            var transaction = await _dbContext.Database
             .BeginTransactionAsync(cancellationToken)
             .ConfigureAwait(false);

            return new ProjectsModuleTransactionScope(
                _dbContext,
                transaction);
        }

        public IReadOnlyCollection<IDomainEvent> CollectDomainEvents()
        {
            return _dbContext.ChangeTracker
            .Entries<Project>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToArray();
        }

        public void ClearDomainEvents(
            IReadOnlyCollection<IDomainEvent> domainEvents)
        {
            ArgumentNullException.ThrowIfNull(domainEvents);

            foreach (var entry in _dbContext.ChangeTracker.Entries<Project>())
            {
                entry.Entity.ClearDomainEvents(domainEvents);
            }
        }
    }
}
