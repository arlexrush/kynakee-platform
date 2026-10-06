using FluentAssertions;
using Kynakee.Modules.SharedKernel.Domain;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Domain;

public class GlobalAggregateRootTests
{
    [Fact]
    public void ConstructorShouldInitializeGlobalEntityProperties()
    {
        var id = Guid.NewGuid();
        var ownerTenantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();

        var aggregate = new TestGlobalAggregate(id, ownerTenantId, ownerUserId);

        aggregate.Id.Should().Be(id);
        aggregate.OwnerTenantId.Should().Be(ownerTenantId);
        aggregate.OwnerUserId.Should().Be(ownerUserId);
        aggregate.CreatedBy.Should().Be(ownerUserId);
        aggregate.IsDeleted.Should().BeFalse();
        aggregate.DeletedAt.Should().BeNull();
        aggregate.CreatedAt.Should().NotBe(default);
        aggregate.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public void AddingDomainEventShouldExposeEvent()
    {
        var aggregate = CreateAggregate();
        var domainEvent = CreateDomainEvent();

        aggregate.Raise(domainEvent);

        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeSameAs(domainEvent);
    }

    [Fact]
    public void ClearingDispatchedEventsShouldPreserveUndispatchedEvents()
    {
        var aggregate = CreateAggregate();
        var dispatchedEvent = CreateDomainEvent();
        var pendingEvent = CreateDomainEvent();
        aggregate.Raise(dispatchedEvent);
        aggregate.Raise(pendingEvent);

        aggregate.ClearDomainEvents([dispatchedEvent]);

        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeSameAs(pendingEvent);
    }

    [Fact]
    public void ClearDomainEventsShouldRemoveAllEvents()
    {
        var aggregate = CreateAggregate();
        aggregate.Raise(CreateDomainEvent());

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    private static TestGlobalAggregate CreateAggregate() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static TestDomainEvent CreateDomainEvent() =>
        new(Guid.NewGuid(), DateTime.UtcNow);

    private sealed record TestDomainEvent(Guid Id, DateTime OccurredOn) : IDomainEvent;

    private sealed class TestGlobalAggregate : GlobalAggregateRoot<Guid>
    {
        public TestGlobalAggregate(Guid id, Guid? ownerTenantId, Guid? ownerUserId)
            : base(id, ownerTenantId, ownerUserId)
        {
        }

        public void Raise(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);
    }
}
