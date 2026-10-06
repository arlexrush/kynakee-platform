using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public interface IDomainEventCollector
    {
        IReadOnlyCollection<IDomainEvent> Collect(
        Type requestType);

        void Clear(
        Type requestType,
        IReadOnlyCollection<IDomainEvent> domainEvents);
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
