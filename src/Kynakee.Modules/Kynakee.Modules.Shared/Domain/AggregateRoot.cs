namespace Kynakee.Modules.SharedKernel.Domain
{
    /// <summary>
    /// Base class for ALL aggregate roots in the Kynakee platform.
    /// Inherits BaseEntity and adds domain event management.
    ///
    /// Only aggregate roots can raise domain events (DDD rule).
    /// Entities inside the aggregate (WorkItem, Schedule, Valuation, etc.)
    /// MUST NOT raise domain events directly — they delegate to the aggregate root.
    ///
    /// Aggregate roots in Kynakee:
    /// - Project       (Projects module — core aggregate)
    /// - CreditAccount (Billing module)
    /// - Tenant        (Identity module)
    /// - User          (Identity module)
    ///
    /// Domain events are collected here and dispatched by
    /// DomainEventDispatchBehavior AFTER the transaction commits (ADR-006).
    /// </summary>
    /// <typeparam name="TId">Type of the aggregate root identifier.</typeparam>

    public abstract class AggregateRoot <TId> : BaseEntity<TId>, IDomainEventSource
    {
        // ── Domain Events ─────────────────────────────────────────────────────────

        private readonly List<IDomainEvent> _domainEvents = [];

        /// <summary>
        /// Read-only collection of domain events raised by this aggregate.
        /// Consumed by DomainEventDispatchBehavior after transaction commit.
        /// </summary>
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        // ── Protected constructors ────────────────────────────────────────────────

        /// <summary>Protected constructor for EF Core.</summary>
        protected AggregateRoot() { }

        /// <summary>
        /// Initializes a new aggregate root with required audit fields.
        /// </summary>
        protected AggregateRoot(TId id, Guid tenantId, Guid? createdBy = null)
            : base(id, tenantId, createdBy) { }

        // ── Domain Event Management ───────────────────────────────────────────────

        /// <summary>
        /// Raises a domain event from this aggregate root.
        /// Call this method inside aggregate methods when a significant
        /// business fact occurs (e.g., ProjectCreated, PhaseAdvanced, OfferGenerated).
        ///
        /// Events are NOT dispatched immediately — they are collected here
        /// and dispatched by DomainEventDispatchBehavior after the DB transaction
        /// commits successfully (ADR-006).
        /// </summary>
        /// <param name="domainEvent">The domain event to raise.</param>
        protected void AddDomainEvent(IDomainEvent domainEvent)
            => _domainEvents.Add(domainEvent);

        /// <summary>
        /// Clears all collected domain events.
        /// Called by DomainEventDispatchBehavior after events are dispatched.
        /// </summary>
        public void ClearDomainEvents()
            => _domainEvents.Clear();


        public void ClearDomainEvents(
            IReadOnlyCollection<IDomainEvent> dispatchedEvents)
        {
            ArgumentNullException.ThrowIfNull(dispatchedEvents);

            var dispatched = new HashSet<IDomainEvent>(
                dispatchedEvents,
                ReferenceEqualityComparer.Instance);

            _domainEvents.RemoveAll(dispatched.Contains);
        }

        // ── Downstream Invalidation ───────────────────────────────────────────────

        /// <summary>
        /// Invalidates downstream results when a upstream entity changes.
        ///
        /// MANDATORY: Override this method in aggregates where modifying
        /// an entity invalidates dependent results.
        ///
        /// Example in Project aggregate:
        /// Modifying a WorkItem MUST invalidate Valuation and Schedule
        /// because they depend on WorkItem quantities and APU assignments.
        /// Copilot Rule #5: WorkItem modification MUST call ResetDependentResults().
        /// </summary>
        protected virtual void ResetDependentResults() { }
    }
}
