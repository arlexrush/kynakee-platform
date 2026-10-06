using FluentAssertions;
using Kynakee.Modules.SharedKernel.Integration;
using Xunit;

namespace Kynakee.ContractTests.SharedKernel
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class IntegrationEventContractTests
    {
        [Fact]
        public void IntegrationEventShouldExposeIdProperty()
        {
            var property = typeof(IIntegrationEvent)
                .GetProperty(nameof(IIntegrationEvent.Id));

            property.Should().NotBeNull();
            property!.PropertyType.Should().Be<Guid>();
            property.GetMethod.Should().NotBeNull();
            property.SetMethod.Should().BeNull();
        }

        [Fact]
        public void IntegrationEventShouldExposeOccurredOnProperty()
        {
            var property = typeof(IIntegrationEvent)
                .GetProperty(nameof(IIntegrationEvent.OccurredOn));

            property.Should().NotBeNull();
            property!.PropertyType.Should().Be<DateTime>();
            property.GetMethod.Should().NotBeNull();
            property.SetMethod.Should().BeNull();
        }

        [Fact]
        public void IntegrationEventShouldExposeTenantIdProperty()
        {
            var property = typeof(IIntegrationEvent)
                .GetProperty(nameof(IIntegrationEvent.TenantId));

            property.Should().NotBeNull();
            property!.PropertyType.Should().Be<Guid>();
            property.GetMethod.Should().NotBeNull();
            property.SetMethod.Should().BeNull();
        }

        [Fact]
        public void IntegrationEventShouldExposeExpectedProperties()
        {
            typeof(IIntegrationEvent)
                .GetProperties()
                .Select(property => property.Name)
                .Should()
                .BeEquivalentTo(
                    nameof(IIntegrationEvent.Id),
                    nameof(IIntegrationEvent.OccurredOn),
                    nameof(IIntegrationEvent.TenantId));
        }

        [Fact]
        public void IntegrationEventBaseShouldBeAbstractAndImplementContract()
        {
            typeof(IntegrationEvent).IsAbstract.Should().BeTrue();

            typeof(IIntegrationEvent)
                .IsAssignableFrom(typeof(IntegrationEvent))
                .Should()
                .BeTrue();
        }

        [Fact]
        public void ConcreteIntegrationEventShouldBeAssignableToContract()
        {
            var tenantId = Guid.NewGuid();
            var integrationEvent = new TestIntegrationEvent(tenantId);

            integrationEvent.Should()
                .BeAssignableTo<IIntegrationEvent>();

            var eventContract = (IIntegrationEvent)integrationEvent;

            eventContract.TenantId.Should().Be(tenantId);
            eventContract.Id.Should().NotBe(Guid.Empty);
            eventContract.OccurredOn.Should().NotBe(default);
        }

        [Fact]
        public void IntegrationEventBaseTenantIdShouldNotHavePublicSetter()
        {
            var property = typeof(IntegrationEvent)
                .GetProperty(nameof(IntegrationEvent.TenantId));

            property.Should().NotBeNull();
            property!.GetSetMethod()
                .Should()
                .BeNull();
        }

        private sealed class TestIntegrationEvent : IntegrationEvent
        {
            public TestIntegrationEvent(Guid tenantId)
            {
                TenantId = tenantId;
            }
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
