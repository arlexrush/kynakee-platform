namespace Kynakee.Modules.SharedKernel.Domain
{
    public interface IDomainEventSource
    {
        IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

        void ClearDomainEvents();

        void ClearDomainEvents(
            IReadOnlyCollection<IDomainEvent> dispatchedEvents);
    }
}
