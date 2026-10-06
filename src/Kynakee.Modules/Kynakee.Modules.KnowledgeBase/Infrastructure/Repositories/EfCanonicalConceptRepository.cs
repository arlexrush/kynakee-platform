using Kynakee.Modules.KnowledgeBase.Domain.Aggregates;
using Kynakee.Modules.KnowledgeBase.Domain.Repositories;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;
using Kynakee.Modules.KnowledgeBase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.KnowledgeBase.Infrastructure.Repositories;

public sealed class EfCanonicalConceptRepository : ICanonicalConceptRepository
{
    private readonly KnowledgeBaseDbContext _dbContext;

    public EfCanonicalConceptRepository(KnowledgeBaseDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<CanonicalConcept?> GetByIdForUpdateAsync(
        CanonicalConceptId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _dbContext.CanonicalConcepts
            .AsTracking()
            .Include(concept => concept.Translations)
            .SingleOrDefaultAsync(concept => concept.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        CanonicalConcept concept,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concept);

        await _dbContext.CanonicalConcepts
            .AddAsync(concept, cancellationToken)
            .ConfigureAwait(false);
    }
}
