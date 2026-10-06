using FluentAssertions;
using Kynakee.Modules.SharedKernel.Domain;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Domain
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class DomainEventTests
    {
        [Fact]
        public void ConstructorShouldGenerateNonEmptyId()
        {
            var domainEvent = CreateDomainEvent();

            domainEvent.Id.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public void ConstructorShouldSetOccurredOn()
        {
            var beforeCreation = DateTime.UtcNow;

            var domainEvent = CreateDomainEvent();

            var afterCreation = DateTime.UtcNow;

            domainEvent.OccurredOn.Should().BeOnOrAfter(beforeCreation);
            domainEvent.OccurredOn.Should().BeOnOrBefore(afterCreation);
        }

        [Fact]
        public void ConstructorShouldSetOccurredOnAsUtc()
        {
            var domainEvent = CreateDomainEvent();

            domainEvent.OccurredOn.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void DifferentEventsShouldHaveDifferentIds()
        {
            var firstEvent = CreateDomainEvent();
            var secondEvent = CreateDomainEvent();

            firstEvent.Id.Should().NotBe(secondEvent.Id);
        }

        [Fact]
        public void EventShouldImplementIDomainEvent()
        {
            var domainEvent = CreateDomainEvent();

            domainEvent.Should().BeAssignableTo<IDomainEvent>();

            var eventContract = (IDomainEvent)domainEvent;

            eventContract.Id.Should().Be(domainEvent.Id);
            eventContract.OccurredOn.Should().Be(domainEvent.OccurredOn);
        }

        [Fact]
        public void DerivedEventShouldPreserveCustomProperties()
        {
            var domainEvent = new TestDomainEvent("Test event");

            domainEvent.Name.Should().Be("Test event");
            domainEvent.Id.Should().NotBe(Guid.Empty);
            domainEvent.OccurredOn.Should().NotBe(default);
        }

        private static TestDomainEvent CreateDomainEvent()
        {
            return new TestDomainEvent("Test event");
        }

        private sealed record TestDomainEvent : DomainEvent
        {
            public TestDomainEvent(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
