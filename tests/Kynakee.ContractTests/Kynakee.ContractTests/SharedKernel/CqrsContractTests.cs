using FluentAssertions;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Xunit;

namespace Kynakee.ContractTests.SharedKernel
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class CqrsContractTests
    {
        [Fact]
        public void GenericCommandShouldInheritTypedMediatRRequest()
        {
            var commandType = typeof(ICommand<string>);
            var expectedRequestType = typeof(IRequest<Result<string>>);

            expectedRequestType
                .IsAssignableFrom(commandType)
                .Should()
                .BeTrue();
        }

        [Fact]
        public void NonGenericCommandShouldInheritNonGenericMediatRRequest()
        {
            var commandType = typeof(ICommand);
            var expectedRequestType = typeof(IRequest<Result>);

            expectedRequestType
                .IsAssignableFrom(commandType)
                .Should()
                .BeTrue();
        }

        [Fact]
        public void QueryShouldInheritTypedMediatRRequest()
        {
            var queryType = typeof(IQuery<string>);
            var expectedRequestType = typeof(IRequest<Result<string>>);

            expectedRequestType
                .IsAssignableFrom(queryType)
                .Should()
                .BeTrue();
        }

        [Fact]
        public void GenericCommandShouldBeMarkerInterface()
        {
            var commandType = typeof(ICommand<string>);

            commandType.GetProperties()
                .Should()
                .BeEmpty();

            commandType.GetMethods()
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void NonGenericCommandShouldBeMarkerInterface()
        {
            var commandType = typeof(ICommand);

            commandType.GetProperties()
                .Should()
                .BeEmpty();

            commandType.GetMethods()
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void QueryShouldBeMarkerInterface()
        {
            var queryType = typeof(IQuery<string>);

            queryType.GetProperties()
                .Should()
                .BeEmpty();

            queryType.GetMethods()
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void CqrsContractsShouldBePublic()
        {
            typeof(ICommand).IsPublic.Should().BeTrue();
            typeof(ICommand<>).IsPublic.Should().BeTrue();
            typeof(IQuery<>).IsPublic.Should().BeTrue();
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
