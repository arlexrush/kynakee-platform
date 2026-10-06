using MediatR;

namespace Kynakee.Modules.SharedKernel.Domain
{
    public interface IDomainEventSubscriber<TEvent> : INotificationHandler<TEvent> where TEvent : DomainEvent
    {

    }
}
