using FluentAssertions;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Application
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class ErrorTypeTests
    {
        [Theory]
        [InlineData(ErrorType.Validation, 0)]
        [InlineData(ErrorType.NotFound, 1)]
        [InlineData(ErrorType.Conflict, 2)]
        [InlineData(ErrorType.Unauthorized, 3)]
        [InlineData(ErrorType.AI, 4)]
        [InlineData(ErrorType.MCP, 5)]
        [InlineData(ErrorType.Credits, 6)]
        [InlineData(ErrorType.Unexpected, 7)]
        public void ShouldContainExpectedErrorTypes(
            ErrorType errorType,
            int expectedValue)
        {
            ((int)errorType).Should().Be(expectedValue);
        }

        [Fact]
        public void ShouldContainExactlyEightValues()
        {
            Enum.GetValues<ErrorType>()
                .Should()
                .HaveCount(8);
        }

        [Fact]
        public void ShouldBeUsableByApplicationError()
        {
            var error = ApplicationError.MCP(
                "MCP_001",
                "MCP provider failed");

            error.Type.Should().Be(ErrorType.MCP);
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
