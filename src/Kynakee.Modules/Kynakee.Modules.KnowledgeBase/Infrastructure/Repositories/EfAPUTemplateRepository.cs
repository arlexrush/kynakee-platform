using Kynakee.Modules.KnowledgeBase.Domain.Aggregates;
using Kynakee.Modules.KnowledgeBase.Domain.Repositories;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;
using Kynakee.Modules.KnowledgeBase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.KnowledgeBase.Infrastructure.Repositories;

public sealed class EfAPUTemplateRepository : IAPUTemplateRepository
{
    private readonly KnowledgeBaseDbContext _dbContext;

    public EfAPUTemplateRepository(KnowledgeBaseDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<APUTemplate?> GetByIdForUpdateAsync(
        APUTemplateId id,
        CancellationToken cancellationToken)
    {
        return _dbContext.APUTemplates
            .AsTracking()
            .AsSplitQuery()
            .Include(template => template.Components)
            .SingleOrDefaultAsync(template => template.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        APUTemplate apuTemplate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apuTemplate);

        await _dbContext.APUTemplates
            .AddAsync(apuTemplate, cancellationToken)
            .ConfigureAwait(false);
    }
}
