namespace Kynakee.Modules.SharedKernel.Domain
{
    /// <summary>
    /// Base class for all domain event handlers in the Kynakee platform.
    /// Implements IDomainEventHandler providing a consistent base for handlers.
    ///
    /// Inherit from this class to create domain event handlers.
    /// The Handle method is called by MediatR via DomainEventDispatchBehavior
    /// after the transaction commits successfully.
    ///
    /// Rules:
    /// - Handlers MUST be side-effect only (send emails, publish integration events, update read models)
    /// - Handlers MUST NOT modify the aggregate that raised the event
    /// - Handlers MUST NOT throw exceptions for business logic — use Result pattern
    /// - For cross-module communication, publish an IntegrationEvent via MassTransit Outbox (ADR-006)
    /// </summary>
    /// <example>
    /// public sealed class ProjectCreatedHandler : DomainEventHandler{ProjectCreatedEvent}
    /// {
    ///     private readonly IPublishEndpoint _publisher;
    ///
    ///     public ProjectCreatedHandler(IPublishEndpoint publisher)
    ///         => _publisher = publisher;
    ///
    ///     public override async Task Handle(
    ///         ProjectCreatedEvent notification,
    ///         CancellationToken cancellationToken)
    ///     {
    ///         await _publisher.Publish(
    ///             new ProjectCreatedIntegrationEvent(
    ///                 notification.ProjectId,
    ///                 notification.TenantId),
    ///             cancellationToken);
    ///     }
    /// }
    /// </example>
    /// <typeparam name="TEvent">The domain event type this handler processes.</typeparam>
    public abstract class DomainEventSubscriber<TEvent> : IDomainEventSubscriber<TEvent> where TEvent : DomainEvent
    {
        /// <summary>
        /// Handles the domain event.
        /// Implement this method to define the side effects of the event.
        /// </summary>
        public abstract Task Handle(TEvent notification, CancellationToken cancellationToken);
        
    }
}
