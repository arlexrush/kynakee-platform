using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence;


/// <summary>
/// Representa un ámbito de transacción para el módulo Projects que coordina el guardado del ProjectsDbContext y el
/// commit/rollback de la transacción de base de datos subyacente.
/// </summary>
/// <remarks>CommitAsync guarda los cambios pendientes en el DbContext y confirma la transacción. RollbackAsync
/// revierte la transacción y limpia el ChangeTracker para no conservar entidades mutadas ni eventos de una operación
/// fallida. DisposeAsync delega la liberación de recursos a la transacción subyacente. Las operaciones CommitAsync y
/// RollbackAsync son idempotentes: si el ámbito ya se ha completado, no realizan ninguna acción adicional; tras
/// completarse, el ámbito no debe reutilizarse.</remarks>
internal sealed class ProjectsModuleTransactionScope :
    IModuleTransactionScope
{
    private readonly ProjectsDbContext _dbContext;
    private readonly IDbContextTransaction _transaction;
    private bool _completed;

    internal ProjectsModuleTransactionScope(
        ProjectsDbContext dbContext,
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

        // No conservar entidades mutadas ni eventos de una operación fallida.
        _dbContext.ChangeTracker.Clear();
        _completed = true;
    }

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
