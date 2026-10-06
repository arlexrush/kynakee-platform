using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kynakee.Modules.KnowledgeBase.Infrastructure.Persistence;

internal sealed class KnowledgeBaseModuleTransactionScope : IModuleTransactionScope
{
    private readonly KnowledgeBaseDbContext _dbContext;
    private readonly IDbContextTransaction _transaction;
    private bool _completed;

    internal KnowledgeBaseModuleTransactionScope(
        KnowledgeBaseDbContext dbContext,
        IDbContextTransaction transaction)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
        _transaction = transaction ??
            throw new ArgumentNullException(nameof(transaction));
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        await _transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);

        _completed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken)
            .ConfigureAwait(false);

        _dbContext.ChangeTracker.Clear();
        _completed = true;
    }

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}