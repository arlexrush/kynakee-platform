using FluentAssertions;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;
using Kynakee.Modules.SharedKernel.Integration;
using Xunit;
using System.Reflection;

namespace Kynakee.ArchitectureTests
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class SharedKernelArchitectureTests
    {
        private static readonly Assembly SharedKernelAssembly =
            typeof(ResultFactory).Assembly;

        [Fact]
        public void SharedKernelTypesShouldBelongToSameAssembly()
        {
            var sharedKernelTypes = new[]
            {
                typeof(ResultFactory),
                typeof(BaseEntity<>),
                typeof(BotMessageReceived),
                typeof(IntegrationEvent)
            };

            sharedKernelTypes
                .Select(type => type.Assembly)
                .Should()
                .OnlyContain(assembly => assembly == SharedKernelAssembly);
        }

        [Fact]
        public void PublicTypesShouldUseSharedKernelNamespace()
        {
            var invalidTypes = SharedKernelAssembly
                .GetExportedTypes()
                .Where(type =>
                    type.Namespace is null ||
                    !type.Namespace.StartsWith(
                        "Kynakee.Modules.SharedKernel",
                        StringComparison.Ordinal))
                .ToArray();

            invalidTypes.Should().BeEmpty();
        }

        [Fact]
        public void NoPublicTypeShouldUseLegacyContracsNamespace()
        {
            var legacyTypes = SharedKernelAssembly
                .GetExportedTypes()
                .Where(type =>
                    type.Namespace?.StartsWith(
                        "Kynakee.Modules.SharedKernel.Contracs",
                        StringComparison.Ordinal) == true)
                .ToArray();

            legacyTypes.Should().BeEmpty();
        }

        [Fact]
        public void ContractsShouldUseContractsNamespace()
        {
            var contractTypes = new[]
            {
                typeof(ITenantContext),
                typeof(IRequiresCredits),
                typeof(IModuleEndpoints),
                typeof(BotMessageReceived)
            };

            contractTypes
                .Select(type => type.Namespace)
                .Should()
                .OnlyContain(namespaceName =>
                    namespaceName == "Kynakee.Modules.SharedKernel.Contracts");
        }

        [Fact]
        public void CoreTypesShouldBeInExpectedNamespaces()
        {
            typeof(ResultFactory).Namespace
                .Should()
                .Be("Kynakee.Modules.SharedKernel.Application");

            typeof(BaseEntity<>).Namespace
                .Should()
                .Be("Kynakee.Modules.SharedKernel.Domain");

            typeof(AggregateRoot<>).Namespace
                .Should()
                .Be("Kynakee.Modules.SharedKernel.Domain");

            typeof(DomainEvent).Namespace
                .Should()
                .Be("Kynakee.Modules.SharedKernel.Domain");

            typeof(IntegrationEvent).Namespace
                .Should()
                .Be("Kynakee.Modules.SharedKernel.Integration");
        }

        [Fact]
        public void AggregateRootShouldInheritBaseEntity()
        {
            var baseType = typeof(AggregateRoot<>).BaseType;

            baseType.Should().NotBeNull();
            baseType!.IsGenericType.Should().BeTrue();
            baseType.GetGenericTypeDefinition()
                .Should()
                .Be(typeof(BaseEntity<>));
        }

        [Fact]
        public void DomainEventShouldImplementIDomainEvent()
        {
            typeof(IDomainEvent)
                .IsAssignableFrom(typeof(DomainEvent))
                .Should()
                .BeTrue();
        }

        [Fact]
        public void IntegrationEventShouldImplementIIntegrationEvent()
        {
            typeof(IIntegrationEvent)
                .IsAssignableFrom(typeof(IntegrationEvent))
                .Should()
                .BeTrue();
        }

        [Fact]
        public void CqrsContractsShouldBeInApplicationNamespace()
        {
            var cqrsTypes = new[]
            {
                typeof(ICommand),
                typeof(ICommand<>),
                typeof(IQuery<>)
            };

            cqrsTypes
                .Select(type => type.Namespace)
                .Should()
                .OnlyContain(namespaceName =>
                    namespaceName == "Kynakee.Modules.SharedKernel.Application");
        }

        [Fact]
        public void Class1ShouldNotExistAsPublicScaffoldType()
        {
            var scaffoldType = SharedKernelAssembly
                .GetExportedTypes()
                .SingleOrDefault(type =>
                    type.FullName == "Kynakee.Modules.SharedKernel.Class1");

            scaffoldType.Should().BeNull(
                "el archivo Class1.cs es un tipo residual del scaffolding");
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
