using FluentAssertions;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Application
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class ResultTests
    {
        [Fact]
        public void GenericSuccessShouldContainValueAndSuccessState()
        {
            var result = ResultFactory.Success(42);

            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Value.Should().Be(42);
            result.Error.Should().BeNull();
        }

        [Fact]
        public void GenericFailureShouldContainErrorAndFailureState()
        {
            var error = ApplicationError.NotFound(
                "TEST_404",
                "Resource not found");

            var result = ResultFactory.Failure<int>(error);

            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Value.Should().Be(default(int));
            result.Error.Should().BeSameAs(error);
        }

        [Fact]
        public void GenericMatchOnSuccessShouldExecuteOnlySuccessCallback()
        {
            var result = ResultFactory.Success(42);
            var successCallbackCalled = false;
            var failureCallbackCalled = false;

            var matchedValue = result.Match(
                value =>
                {
                    successCallbackCalled = true;
                    return $"Value: {value}";
                },
                _ =>
                {
                    failureCallbackCalled = true;
                    return "Failure";
                });

            matchedValue.Should().Be("Value: 42");
            successCallbackCalled.Should().BeTrue();
            failureCallbackCalled.Should().BeFalse();
        }

        [Fact]
        public void GenericMatchOnFailureShouldExecuteOnlyFailureCallback()
        {
            var error = ApplicationError.NotFound(
                "TEST_404",
                "Resource not found");

            var result = ResultFactory.Failure<int>(error);
            var successCallbackCalled = false;
            var failureCallbackCalled = false;

            var matchedValue = result.Match(
                _ =>
                {
                    successCallbackCalled = true;
                    return "Success";
                },
                receivedError =>
                {
                    failureCallbackCalled = true;
                    return receivedError.Code;
                });

            matchedValue.Should().Be("TEST_404");
            successCallbackCalled.Should().BeFalse();
            failureCallbackCalled.Should().BeTrue();
        }

        [Fact]
        public void GenericMatchShouldThrowWhenSuccessCallbackIsNull()
        {
            var result = ResultFactory.Success(42);

            var action = () => result.Match<string>(
                null!,
                _ => "Failure");

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GenericMatchShouldThrowWhenFailureCallbackIsNull()
        {
            var result = ResultFactory.Success(42);

            var action = () => result.Match<string>(
                _ => "Success",
                null!);

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void MapOnSuccessShouldTransformValue()
        {
            var result = ResultFactory.Success(10);

            var mappedResult = result.Map(value => $"Value: {value}");

            mappedResult.IsSuccess.Should().BeTrue();
            mappedResult.IsFailure.Should().BeFalse();
            mappedResult.Value.Should().Be("Value: 10");
            mappedResult.Error.Should().BeNull();
        }

        [Fact]
        public void MapOnFailureShouldPropagateSameError()
        {
            var error = ApplicationError.Conflict(
                "TEST_409",
                "Operation conflicts with current state");

            var result = ResultFactory.Failure<int>(error);

            var mappedResult = result.Map(value => $"Value: {value}");

            mappedResult.IsSuccess.Should().BeFalse();
            mappedResult.IsFailure.Should().BeTrue();
            mappedResult.Value.Should().BeNull();
            mappedResult.Error.Should().BeSameAs(error);
        }

        [Fact]
        public void MapShouldThrowWhenMapperIsNull()
        {
            var result = ResultFactory.Success(42);

            var action = () => result.Map<string>(null!);

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void NonGenericSuccessShouldHaveSuccessState()
        {
            var result = ResultFactory.Ok();

            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Error.Should().BeNull();
        }

        [Fact]
        public void NonGenericFailureShouldContainErrorAndFailureState()
        {
            var error = ApplicationError.Unauthorized(
                "TEST_401",
                "Access denied");

            var result = ResultFactory.Failure(error);

            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeSameAs(error);
        }

        [Fact]
        public void NonGenericMatchOnSuccessShouldExecuteOnlySuccessCallback()
        {
            var result = ResultFactory.Ok();
            var successCallbackCalled = false;
            var failureCallbackCalled = false;

            var matchedValue = result.Match(
                () =>
                {
                    successCallbackCalled = true;
                    return "Success";
                },
                _ =>
                {
                    failureCallbackCalled = true;
                    return "Failure";
                });

            matchedValue.Should().Be("Success");
            successCallbackCalled.Should().BeTrue();
            failureCallbackCalled.Should().BeFalse();
        }

        [Fact]
        public void NonGenericMatchOnFailureShouldExecuteOnlyFailureCallback()
        {
            var error = ApplicationError.AI(
                "TEST_AI",
                "AI provider failed");

            var result = ResultFactory.Failure(error);
            var successCallbackCalled = false;
            var failureCallbackCalled = false;

            var matchedValue = result.Match(
                () =>
                {
                    successCallbackCalled = true;
                    return "Success";
                },
                receivedError =>
                {
                    failureCallbackCalled = true;
                    return receivedError.Code;
                });

            matchedValue.Should().Be("TEST_AI");
            successCallbackCalled.Should().BeFalse();
            failureCallbackCalled.Should().BeTrue();
        }

        [Fact]
        public void NonGenericMatchShouldThrowWhenSuccessCallbackIsNull()
        {
            var result = ResultFactory.Ok();

            var action = () => result.Match<string>(
                null!,
                _ => "Failure");

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void NonGenericMatchShouldThrowWhenFailureCallbackIsNull()
        {
            var result = ResultFactory.Ok();

            var action = () => result.Match<string>(
                () => "Success",
                null!);

            action.Should().Throw<ArgumentNullException>();
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
