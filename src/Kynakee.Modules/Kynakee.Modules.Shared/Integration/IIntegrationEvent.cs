namespace Kynakee.Modules.SharedKernel.Integration
{
    /// <summary>
    /// Marker interface for all integration events in the Kynakee platform.
    /// Integration events represent facts that need to be communicated
    /// across module boundaries (ADR-005, ADR-006).
    ///
    /// Design rules:
    /// - Integration events are published via MassTransit Outbox ONLY (ADR-006)
    /// - NEVER publish integration events directly — always via Outbox
    /// - Integration events are raised by DomainEventHandlers AFTER transaction commits
    /// - Each module consumes only the integration events it needs
    /// - Integration events MUST be immutable — never modify after creation
    ///
    /// Difference from DomainEvent:
    /// - DomainEvent  → intra-aggregate, same transaction, same module
    /// - IntegrationEvent → inter-module, via RabbitMQ + MassTransit Outbox
    /// </summary>
    public interface IIntegrationEvent
    {
        /// <summary>Unique identifier of this event instance.</summary>
        Guid Id { get; }

        /// <summary>UTC timestamp when this event occurred.</summary>
        DateTime OccurredOn { get; }

        /// <summary>
        /// Tenant identifier.
        /// MANDATORY on all integration events for multi-tenant routing (ADR-007).
        /// </summary>
        Guid TenantId { get; }
    }
}
