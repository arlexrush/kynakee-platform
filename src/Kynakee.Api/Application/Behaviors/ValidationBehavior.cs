using FluentValidation;
using FluentValidation.Results;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Api.Application.Behaviors
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IValidator<TRequest>[] _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators.ToArray();
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (_validators.Length == 0)
            {
                ArgumentNullException.ThrowIfNull(next);
                return await next(cancellationToken).ConfigureAwait(false);
            }
                       

            var validationResults = await Task.WhenAll(_validators.Select(validator => validator.ValidateAsync(request, cancellationToken))).ConfigureAwait(false);

            var failures = validationResults
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .ToArray();

            if (failures.Length == 0)
            {
                ArgumentNullException.ThrowIfNull(next);
                return await next(cancellationToken).ConfigureAwait(false);
            }

            var message = string.Join(
                "; ",
                failures.Select(failure =>
                    $"{failure.PropertyName}: {failure.ErrorMessage}"));

            var error = ApplicationError.Validation(
                "VALIDATION_001",
                message);

            return ResultResponseFactory.CreateFailure<TResponse>(error);
        }
        
    }

    internal static class ResultResponseFactory
    {
        public static TResponse CreateFailure<TResponse>(
            ApplicationError error)
        {
            if (typeof(TResponse) == typeof(Result))
            {
                return (TResponse)(object)ResultFactory.Failure(error);
            }

            if (typeof(TResponse).IsGenericType &&
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var responseType = typeof(TResponse).GetGenericArguments()[0];

                var failureMethod = typeof(ResultFactory)
                    .GetMethods()
                    .Single(method => method.Name == nameof(ResultFactory.Failure) &&
                        method.IsGenericMethodDefinition &&
                        method.GetGenericArguments().Length == 1 &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType == typeof(ApplicationError))
                    .MakeGenericMethod(responseType);

                return (TResponse)failureMethod.Invoke(
                    null,
                    [error])!;
            }

            throw new InvalidOperationException(
                $"ValidationBehavior only supports {nameof(Result)} responses. " +
                $"Actual response type: {typeof(TResponse).FullName}.");
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
