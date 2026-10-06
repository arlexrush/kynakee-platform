using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kynakee.Modules.Mcp.Infrastructure.Persistence;

internal sealed class McpModuleTransactionScope : IModuleTransactionScope
{
    private readonly McpDbContext _dbContext;
    private readonly IDbContextTransaction _transaction;
    private bool _completed;

    internal McpModuleTransactionScope(
        McpDbContext dbContext,
        IDbContextTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(transaction);
        _dbContext = dbContext;
        _transaction = transaction;
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
