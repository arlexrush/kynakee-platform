using FluentAssertions;
using Kynakee.Modules.SharedKernel.Domain;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Domain
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class AggregateRootTests
    {
        [Fact]
        public void NewAggregateShouldHaveEmptyDomainEvents()
        {
            var aggregate = CreateAggregate();

            aggregate.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void ConstructorShouldInitializeBaseEntityProperties()
        {
            var id = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();

            var aggregate = new TestAggregate(id, tenantId, createdBy);

            aggregate.Id.Should().Be(id);
            aggregate.TenantId.Should().Be(tenantId);
            aggregate.CreatedBy.Should().Be(createdBy);
            aggregate.IsDeleted.Should().BeFalse();
            aggregate.DeletedAt.Should().BeNull();
            aggregate.CreatedAt.Should().NotBe(default);
            aggregate.UpdatedAt.Should().NotBe(default);
        }

        [Fact]
        public void AddingDomainEventShouldAddDomainEvent()
        {
            var aggregate = CreateAggregate();
            var domainEvent = CreateDomainEvent();

            aggregate.Raise(domainEvent);

            aggregate.DomainEvents.Should().ContainSingle();
        }

        [Fact]
        public void AddingDomainEventShouldPreserveEventInstance()
        {
            var aggregate = CreateAggregate();
            var domainEvent = CreateDomainEvent();

            aggregate.Raise(domainEvent);

            aggregate.DomainEvents.Single().Should().BeSameAs(domainEvent);
        }

        [Fact]
        public void AddingDomainEventShouldPreserveEventOrder()
        {
            var aggregate = CreateAggregate();
            var firstEvent = CreateDomainEvent();
            var secondEvent = CreateDomainEvent();

            aggregate.Raise(firstEvent);
            aggregate.Raise(secondEvent);

            aggregate.DomainEvents.Should()
                .ContainInOrder(firstEvent, secondEvent);
        }

        [Fact]
        public void DomainEventsShouldExposeReadOnlyCollection()
        {
            var aggregate = CreateAggregate();
            var domainEvent = CreateDomainEvent();

            aggregate.Raise(domainEvent);

            var events = aggregate.DomainEvents as ICollection<IDomainEvent>;

            events.Should().NotBeNull();
            events!.IsReadOnly.Should().BeTrue();
        }

        [Fact]
        public void ClearDomainEventsShouldRemoveAllEvents()
        {
            var aggregate = CreateAggregate();

            aggregate.Raise(CreateDomainEvent());
            aggregate.Raise(CreateDomainEvent());

            aggregate.ClearDomainEvents();

            aggregate.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void ClearDomainEventsOnEmptyAggregateShouldRemainEmpty()
        {
            var aggregate = CreateAggregate();

            var action = () => aggregate.ClearDomainEvents();

            action.Should().NotThrow();
            aggregate.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void ResetDependentResultsShouldBeOverridable()
        {
            var aggregate = CreateAggregate();

            aggregate.Reset();

            aggregate.ResetWasCalled.Should().BeTrue();
        }

        private static TestAggregate CreateAggregate()
            => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        private static TestDomainEvent CreateDomainEvent()
            => new TestDomainEvent(Guid.NewGuid(), DateTime.UtcNow);

        private sealed record TestDomainEvent(
            Guid Id,
            DateTime OccurredOn) : IDomainEvent;

        private sealed class TestAggregate : AggregateRoot<Guid>
        {
            public bool ResetWasCalled { get; private set; }

            public TestAggregate(Guid id, Guid tenantId, Guid? createdBy = null)
            : base(id, tenantId, createdBy)
            {

            }

            public void Raise(IDomainEvent domainEvent)
            {
                AddDomainEvent(domainEvent);
            }

            public void Reset()
            {
                ResetDependentResults();
            }

            protected override void ResetDependentResults()
            {
                ResetWasCalled = true;
            }

        }
    }
        

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
