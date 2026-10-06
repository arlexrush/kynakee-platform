namespace Kynakee.Modules.SharedKernel.Contracts
{
    public interface IModuleTransactionScope : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken);

        Task RollbackAsync(CancellationToken cancellationToken);
    }
}
