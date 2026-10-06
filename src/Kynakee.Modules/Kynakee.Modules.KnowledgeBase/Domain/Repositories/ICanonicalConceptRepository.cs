using Kynakee.Modules.KnowledgeBase.Domain.Aggregates;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;

namespace Kynakee.Modules.KnowledgeBase.Domain.Repositories;

public interface ICanonicalConceptRepository
{
    Task<CanonicalConcept?> GetByIdForUpdateAsync(
        CanonicalConceptId id,
        CancellationToken cancellationToken);

    Task AddAsync(
        CanonicalConcept concept,
        CancellationToken cancellationToken);
}
