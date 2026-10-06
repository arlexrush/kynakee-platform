using FluentAssertions;
using Kynakee.Modules.SharedKernel.Domain;
using MediatR;
using Xunit;

namespace Kynakee.ContractTests.SharedKernel
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class DomainEventContractTests
    {
        [Fact]
        public void DomainEventShouldInheritMediatRNotification()
        {
            typeof(INotification)
                .IsAssignableFrom(typeof(IDomainEvent))
                .Should()
                .BeTrue();
        }

        [Fact]
        public void DomainEventShouldExposeIdProperty()
        {
            var property = typeof(IDomainEvent).GetProperty(nameof(IDomainEvent.Id));

            property.Should().NotBeNull();
            property!.PropertyType.Should().Be<Guid>();
            property.GetMethod.Should().NotBeNull();
            property.SetMethod.Should().BeNull();
        }

        [Fact]
        public void DomainEventShouldExposeOccurredOnProperty()
        {
            var property = typeof(IDomainEvent)
                .GetProperty(nameof(IDomainEvent.OccurredOn));

            property.Should().NotBeNull();
            property!.PropertyType.Should().Be<DateTime>();
            property.GetMethod.Should().NotBeNull();
            property.SetMethod.Should().BeNull();
        }

        [Fact]
        public void DomainEventBaseShouldBeAbstractAndImplementContract()
        {
            typeof(DomainEvent).IsAbstract.Should().BeTrue();

            typeof(IDomainEvent)
                .IsAssignableFrom(typeof(DomainEvent))
                .Should()
                .BeTrue();
        }

        [Fact]
        public void DomainEventSubscriberShouldInheritMediatRNotificationHandler()
        {
            var subscriberType =
                typeof(IDomainEventSubscriber<TestDomainEvent>);

            var handlerType =
                typeof(INotificationHandler<TestDomainEvent>);

            handlerType
                .IsAssignableFrom(subscriberType)
                .Should()
                .BeTrue();
        }

        [Fact]
        public void DomainEventSubscriberShouldRestrictEventTypeToDomainEvent()
        {
            var eventTypeParameter = typeof(IDomainEventSubscriber<>)
                .GetGenericArguments()
                .Single();

            eventTypeParameter
                .GetGenericParameterConstraints()
                .Should()
                .Contain(typeof(DomainEvent));
        }

        [Fact]
        public async Task DomainEventSubscriberShouldBeImplementable()
        {
            var subscriber = new TestDomainEventSubscriber();
            var domainEvent = new TestDomainEvent();

            subscriber.Should()
                .BeAssignableTo<IDomainEventSubscriber<TestDomainEvent>>();

            subscriber.Should()
                .BeAssignableTo<INotificationHandler<TestDomainEvent>>();

            await subscriber.Handle(domainEvent, CancellationToken.None);
        }

        private sealed record TestDomainEvent : DomainEvent
        {
        }

        private sealed class TestDomainEventSubscriber
            : IDomainEventSubscriber<TestDomainEvent>
        {
            public Task Handle(
                TestDomainEvent notification,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
