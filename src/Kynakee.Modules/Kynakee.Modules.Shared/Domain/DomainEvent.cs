namespace Kynakee.Modules.SharedKernel.Domain
{
    /// <summary>
    /// Base class for all domain events in the Kynakee platform.
    /// Implements IDomainEvent providing default values for Id and OccurredOn.
    ///
    /// Usage: Inherit from this class to create domain events.
    /// Domain events are raised by AggregateRoot and dispatched by
    /// DomainEventDispatchBehavior after the transaction commits.
    /// </summary>
    /// <example>
    /// public sealed class ProjectCreatedEvent : DomainEvent
    /// {
    ///     public Guid   ProjectId { get; }
    ///     public Guid   TenantId  { get; }
    ///     public string Name      { get; }
    ///
    ///     public ProjectCreatedEvent(Guid projectId, Guid tenantId, string name)
    ///     {
    ///         ProjectId = projectId;
    ///         TenantId  = tenantId;
    ///         Name      = name;
    ///     }
    /// }
    /// </example>
    public abstract record DomainEvent : IDomainEvent
    {
        /// <inheritdoc />
        public Guid Id { get; } = Guid.NewGuid();

        /// <inheritdoc />
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
