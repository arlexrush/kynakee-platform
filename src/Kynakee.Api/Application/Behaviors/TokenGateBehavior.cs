using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Api.Application.Behaviors
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    public sealed class TokenGateBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    {
        private readonly ITenantContext _tenantContext;
        private readonly ITokenGateService? _tokenGateService;

        public TokenGateBehavior(
            ITenantContext tenantContext,
            ITokenGateService? tokenGateService = null)
        {
            _tenantContext = tenantContext;
            _tokenGateService = tokenGateService;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(next);

            if (request is not IRequiresCredits creditRequest)
            {
                return await next(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (creditRequest.CreditsRequired <= 0)
            {
                var invalidCreditsError = ApplicationError.Validation(
                    "CREDITS_001",
                    "The command must specify a positive number of required credits.");

                return ResultResponseFactory.CreateFailure<TResponse>(
                    invalidCreditsError);
            }

            if (!_tenantContext.IsAuthenticated ||
                _tenantContext.TenantId == Guid.Empty)
            {
                var tenantError = ApplicationError.Unauthorized(
                    "CREDITS_002",
                    "A valid authenticated tenant is required for credit-based operations.");

                return ResultResponseFactory.CreateFailure<TResponse>(
                    tenantError);
            }

            if (_tokenGateService is null)
            {
                var unavailableError = ApplicationError.Credits(
                    "CREDITS_003",
                    "The credit service is not available.");

                return ResultResponseFactory.CreateFailure<TResponse>(
                    unavailableError);
            }

            var operationId = Guid.NewGuid();

            var reservationResult = await _tokenGateService.ReserveAsync(
                _tenantContext.TenantId,
                creditRequest.CreditsRequired,
                operationId,
                cancellationToken).ConfigureAwait(false);

            if (reservationResult.IsFailure)
            {
                return ResultResponseFactory.CreateFailure<TResponse>(
                    reservationResult.Error!);
            }

            try
            {
                var response = await next(cancellationToken)
                    .ConfigureAwait(false);

                if (!ResultResponseInspector.IsSuccess(response))
                {
                    await _tokenGateService.ReleaseAsync(
                        _tenantContext.TenantId,
                        operationId,
                        CancellationToken.None).ConfigureAwait(false);

                    return response;
                }

                var consumeResult = await _tokenGateService.ConsumeAsync(
                    _tenantContext.TenantId,
                    operationId,
                    CancellationToken.None).ConfigureAwait(false);

                if (consumeResult.IsFailure)
                {
                    return ResultResponseFactory.CreateFailure<TResponse>(
                        consumeResult.Error!);
                }

                return response;
            }
            catch
            {
                await _tokenGateService.ReleaseAsync(
                    _tenantContext.TenantId,
                    operationId,
                    CancellationToken.None).ConfigureAwait(false);

                throw;
            }
        }

        internal static class ResultResponseInspector
        {
            public static bool IsSuccess<TResult>(TResult response)
            {
                ArgumentNullException.ThrowIfNull(response);

                var responseType = response.GetType();
                var isSuccessProperty = responseType.GetProperty(nameof(Result.IsSuccess));

                if (isSuccessProperty?.PropertyType != typeof(bool))
                {
                    throw new InvalidOperationException(
                        $"TokenGateBehavior only supports Result responses. " +
                        $"Actual response type: {responseType.FullName}.");
                }

                return (bool)isSuccessProperty.GetValue(response)!;
            }
        }

    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
