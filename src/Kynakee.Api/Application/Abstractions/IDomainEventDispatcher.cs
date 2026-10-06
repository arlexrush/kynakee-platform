using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken);
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
