using MediatR;

namespace Kynakee.Modules.SharedKernel.Domain
{
    /// <summary>
    /// Marker interface for all domain events in the Kynakee platform.
    ///
    /// Design decision: Inherits INotification from MediatR (ADR-004).
    /// This is a conscious architectural coupling — MediatR is a core platform
    /// dependency, not an implementation detail. If MediatR is ever replaced,
    /// this is the single file that needs to change.
    ///
    /// Domain events represent facts that occurred within the domain.
    /// They are dispatched AFTER the transaction commits (DomainEventDispatchBehavior).
    /// </summary>
    public interface IDomainEvent : INotification
    {
        /// <summary>Unique identifier of this event instance.</summary>
        Guid Id { get; }

        /// <summary>UTC timestamp when this event occurred.</summary>
        DateTime OccurredOn { get; }
    }
}
