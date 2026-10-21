using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    /// <summary>
    /// Gestiona el envío asíncrono de eventos de dominio a los manejadores registrados.
    /// </summary>
    /// <remarks>Las implementaciones deben publicar cada evento a los manejadores apropiados de forma
    /// asíncrona. El orden de invocación no está garantizado salvo que la implementación lo especifique. Soporta
    /// cancelación mediante CancellationToken y debe documentar su política frente a errores (propagación, reintentos o
    /// agregación).</remarks>
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken);
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
