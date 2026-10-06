using Kynakee.Modules.SharedKernel.Domain;
using MediatR;

namespace Kynakee.Api.Application.Abstractions
{


#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    /// <summary>
    /// MediatR implementation of the <see cref="IDomainEventDispatcher"/> interface, responsible for dispatching domain events to their respective handlers.
    /// </summary>
    public sealed class MediatRDomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IPublisher _publisher;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatRDomainEventDispatcher"/> class.
        /// </summary>
        /// <param name="publisher">The MediatR publisher used to publish domain events.</param>
        public MediatRDomainEventDispatcher(IPublisher publisher)
        {
            _publisher = publisher;
        }

        /// <summary>
        /// Dispatches the specified domain events to their respective handlers.
        /// </summary>
        /// <param name="domainEvents">The domain events to dispatch.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task DispatchAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(domainEvents);

            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
            }
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
