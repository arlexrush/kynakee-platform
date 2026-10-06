namespace Kynakee.Modules.SharedKernel.Domain;

/// <summary>
/// Base class for global aggregate roots. Inherits global ownership and audit
/// properties from <see cref="GlobalEntity{TId}"/> and adds domain event management.
/// </summary>
/// <typeparam name="TId">Type of the aggregate root identifier.</typeparam>
public abstract class GlobalAggregateRoot<TId> : GlobalEntity<TId>, IDomainEventSource
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected GlobalAggregateRoot()
    {
    }

    protected GlobalAggregateRoot(
        TId id,
        Guid? ownerTenantId,
        Guid? ownerUserId)
        : base(id, ownerTenantId, ownerUserId)
    {
    }

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void ClearDomainEvents(
        IReadOnlyCollection<IDomainEvent> dispatchedEvents)
    {
        ArgumentNullException.ThrowIfNull(dispatchedEvents);

        var dispatched = new HashSet<IDomainEvent>(
            dispatchedEvents,
            ReferenceEqualityComparer.Instance);

        _domainEvents.RemoveAll(dispatched.Contains);
    }
}
