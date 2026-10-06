using Kynakee.Modules.KnowledgeBase.Domain.Aggregates;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;

namespace Kynakee.Modules.KnowledgeBase.Domain.Repositories;

public interface IAPUTemplateRepository
{
    Task<APUTemplate?> GetByIdForUpdateAsync(
        APUTemplateId id,
        CancellationToken cancellationToken);

    Task AddAsync(
        APUTemplate apuTemplate,
        CancellationToken cancellationToken);
}
