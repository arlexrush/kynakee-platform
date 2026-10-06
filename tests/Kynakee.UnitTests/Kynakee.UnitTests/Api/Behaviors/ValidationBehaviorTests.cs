using FluentAssertions;
using FluentValidation;
using Kynakee.Api.Application.Behaviors;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.Api.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task HandleWithoutValidatorsShouldInvokeHandler()
    {
        var behavior = new ValidationBehavior<TestRequest, Result<TestResponse>>(
            Array.Empty<IValidator<TestRequest>>());
        var expected = ResultFactory.Success(new TestResponse("ok"));
        var handlerInvoked = false;

        var result = await behavior.Handle(
            new TestRequest("value"),
            _ =>
            {
                handlerInvoked = true;
                return Task.FromResult(expected);
            },
            CancellationToken.None);

        handlerInvoked.Should().BeTrue();
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task HandleWithValidRequestShouldInvokeHandler()
    {
        var behavior = new ValidationBehavior<TestRequest, Result<TestResponse>>(
            [new TestRequestValidator()]);
        var expected = ResultFactory.Success(new TestResponse("ok"));
        var handlerInvoked = false;

        var result = await behavior.Handle(
            new TestRequest("valid"),
            _ =>
            {
                handlerInvoked = true;
                return Task.FromResult(expected);
            },
            CancellationToken.None);

        handlerInvoked.Should().BeTrue();
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task HandleWithInvalidRequestShouldReturnValidationFailureWithoutInvokingHandler()
    {
        var behavior = new ValidationBehavior<TestRequest, Result<TestResponse>>(
            [new TestRequestValidator()]);
        var handlerInvoked = false;

        var result = await behavior.Handle(
            new TestRequest(string.Empty),
            _ =>
            {
                handlerInvoked = true;
                return Task.FromResult(ResultFactory.Success(new TestResponse("unexpected")));
            },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeEquivalentTo(new
        {
            Code = "VALIDATION_001",
            Type = ErrorType.Validation
        });
        result.Error!.Message.Should().Contain("Value: Value is required.");
        handlerInvoked.Should().BeFalse();
    }

    private sealed record TestRequest(string Value);

    private sealed record TestResponse(string Value);

    private sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(request => request.Value).NotEmpty().WithMessage("Value is required.");
        }
    }
}
