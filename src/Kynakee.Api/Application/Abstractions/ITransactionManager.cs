using Kynakee.Modules.SharedKernel.Contracts;

namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    /// <summary>
    /// Defines a contract for managing transactions.
    /// </summary>
    public interface ITransactionManager
    {
        Task<IModuleTransactionScope> BeginAsync(
        Type requestType,
        CancellationToken cancellationToken);
    }
    
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
