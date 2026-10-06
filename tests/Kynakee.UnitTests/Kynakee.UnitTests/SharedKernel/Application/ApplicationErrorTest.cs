using FluentAssertions;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Application
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class ApplicationErrorTest
    {
        /// <summary>
        /// tests that the Validation method creates an ApplicationError with the correct properties.
        /// </summary>
        [Fact]
        public void ValidationShouldCreateValidationError()
        {
            var error = ApplicationError.Validation("TEST_001", "Validation failed"); // Act 

            error.Code.Should().Be("TEST_001"); // Assert
            error.Message.Should().Be("Validation failed"); // Assert
            error.Type.Should().Be(ErrorType.Validation); // Assert
        }

        /// <summary>
        /// tests that the NotFound method creates an ApplicationError with the correct properties.
        /// </summary>
        [Fact]
        public void NotFoundShouldCreateNotFoundError()
        {
            var error = ApplicationError.NotFound("TEST_002", "Resource not found"); // Act

            error.Code.Should().Be("TEST_002"); // Assert
            error.Message.Should().Be("Resource not found"); // Assert
            error.Type.Should().Be(ErrorType.NotFound); // Assert
        }

        [Fact]
        public void ConflictShouldCreateConflictError()
        {
            var error = ApplicationError.Conflict("TEST_003", "Operation conflicts with current state"); // Act

            error.Code.Should().Be("TEST_003"); // Assert
            error.Message.Should().Be("Operation conflicts with current state");
            error.Type.Should().Be(ErrorType.Conflict);
        }

        [Fact]
        public void UnauthorizedShouldCreateUnauthorizedError()
        {
            var error = ApplicationError.Unauthorized("TEST_004", "Access denied");

            error.Code.Should().Be("TEST_004");
            error.Message.Should().Be("Access denied");
            error.Type.Should().Be(ErrorType.Unauthorized);
        }

        [Fact]
        public void AIShouldCreateAiError()
        {
            var error = ApplicationError.AI("TEST_005", "AI provider failed");

            error.Code.Should().Be("TEST_005");
            error.Message.Should().Be("AI provider failed");
            error.Type.Should().Be(ErrorType.AI);
        }

        [Fact]
        public void MCPShouldCreateMcpError()
        {
            var error = ApplicationError.MCP("TEST_006", "MCP provider failed");

            error.Code.Should().Be("TEST_006");
            error.Message.Should().Be("MCP provider failed");
            error.Type.Should().Be(ErrorType.MCP);
        }

        [Fact]
        public void CreditsShouldCreateCreditsError()
        {
            var error = ApplicationError.Credits("TEST_007", "Insufficient credits");

            error.Code.Should().Be("TEST_007");
            error.Message.Should().Be("Insufficient credits");
            error.Type.Should().Be(ErrorType.Credits);
        }

        [Fact]
        public void UnexpectedShouldCreateUnexpectedError()
        {
            var error = ApplicationError.Unexpected("TEST_008", "Unexpected error");

            error.Code.Should().Be("TEST_008");
            error.Message.Should().Be("Unexpected error");
            error.Type.Should().Be(ErrorType.Unexpected);
        }

        [Fact]
        public void NoneShouldHaveExpectedValues()
        {
            var error = ApplicationError.None;

            error.Code.Should().Be("NONE");
            error.Message.Should().Be("No error");
            error.Type.Should().Be(ErrorType.Validation);
        }

        [Fact]
        public void NullValueShouldHaveExpectedValues()
        {
            var error = ApplicationError.NullValue;

            error.Code.Should().Be("SHARED_001");
            error.Message.Should().Be("A null value was provided.");
            error.Type.Should().Be(ErrorType.Validation);
        }

        [Fact]
        public void AccessDeniedShouldHaveExpectedValues()
        {
            var error = ApplicationError.AccessDenied;

            error.Code.Should().Be("SHARED_002");
            error.Message.Should().Be("Access denied.");
            error.Type.Should().Be(ErrorType.Unauthorized);
        }

        [Fact]
        public void InsufficientCreditsShouldHaveExpectedValues()
        {
            var error = ApplicationError.InsufficientCredits;

            error.Code.Should().Be("SHARED_003");
            error.Message.Should().Be("Insufficient credits to execute this operation.");
            error.Type.Should().Be(ErrorType.Credits);
        }

        [Fact]
        public void ErrorsWithSameValuesShouldBeEqual()
        {
            var first = ApplicationError.Validation("TEST_001", "Validation failed");
            var second = ApplicationError.Validation("TEST_001", "Validation failed");

            first.Should().Be(second);
        }

        [Fact]
        public void ErrorsWithDifferentValuesShouldNotBeEqual()
        {
            var first = ApplicationError.Validation("TEST_001", "Validation failed");
            var second = ApplicationError.Validation("TEST_002", "Validation failed");

            first.Should().NotBe(second);
        }

    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
