using FluentAssertions;
using Kynakee.Modules.SharedKernel.Integration;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Integration
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class IntegrationEventTests
    {
        [Fact]
        public void ConstructorShouldGenerateNonEmptyId()
        {
            var integrationEvent = CreateIntegrationEvent();

            integrationEvent.Id.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public void ConstructorShouldSetOccurredOn()
        {
            var beforeCreation = DateTime.UtcNow;

            var integrationEvent = CreateIntegrationEvent();

            var afterCreation = DateTime.UtcNow;

            integrationEvent.OccurredOn.Should().BeOnOrAfter(beforeCreation);
            integrationEvent.OccurredOn.Should().BeOnOrBefore(afterCreation);
        }

        [Fact]
        public void ConstructorShouldSetOccurredOnAsUtc()
        {
            var integrationEvent = CreateIntegrationEvent();

            integrationEvent.OccurredOn.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void ConstructorShouldPreserveTenantId()
        {
            var tenantId = Guid.NewGuid();

            var integrationEvent = new TestIntegrationEvent(
                tenantId,
                "ProjectCreated");

            integrationEvent.TenantId.Should().Be(tenantId);
        }

        [Fact]
        public void DifferentEventsShouldHaveDifferentIds()
        {
            var firstEvent = CreateIntegrationEvent();
            var secondEvent = CreateIntegrationEvent();

            firstEvent.Id.Should().NotBe(secondEvent.Id);
        }

        [Fact]
        public void EventShouldImplementIIntegrationEvent()
        {
            var integrationEvent = CreateIntegrationEvent();
            var eventContract = (IIntegrationEvent)integrationEvent;

            eventContract.Id.Should().Be(integrationEvent.Id);
            eventContract.OccurredOn.Should().Be(integrationEvent.OccurredOn);
            eventContract.TenantId.Should().Be(integrationEvent.TenantId);
        }

        [Fact]
        public void DerivedEventShouldPreserveCustomProperties()
        {
            var integrationEvent = new TestIntegrationEvent(
                Guid.NewGuid(),
                "ProjectCreated");

            integrationEvent.Operation.Should().Be("ProjectCreated");
        }

        private static TestIntegrationEvent CreateIntegrationEvent()
        {
            return new TestIntegrationEvent(
                Guid.NewGuid(),
                "ProjectCreated");
        }

        private sealed class TestIntegrationEvent : IntegrationEvent
        {
            public TestIntegrationEvent(
                Guid tenantId,
                string operation)
            {
                TenantId = tenantId;
                Operation = operation;
            }

            public string Operation { get; }
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
