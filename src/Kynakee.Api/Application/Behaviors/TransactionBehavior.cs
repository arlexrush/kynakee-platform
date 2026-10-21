using Kynakee.Api.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using System.Security.Cryptography;
using System.Text.Json;


namespace Kynakee.Api.Application.Behaviors
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IPostCommitActionDispatcher _postCommitDispatcher;
        private readonly IHttpIdempotencyProtector? _idempotencyProtector;

        public TransactionBehavior(
            ITransactionManager transactionManager,
            IPostCommitActionDispatcher postCommitDispatcher,
            IHttpIdempotencyProtector? idempotencyProtector = null)
        {
            _transactionManager = transactionManager;
            _postCommitDispatcher = postCommitDispatcher;
            _idempotencyProtector = idempotencyProtector;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(next);

            if (!IsCommand(request))
            {
                return await next(cancellationToken).ConfigureAwait(false);
            }

            var transaction = await _transactionManager.BeginAsync(typeof(TRequest), cancellationToken).ConfigureAwait(false);


            await using (transaction.ConfigureAwait(false))
            {
                var committed = false;

                try
                {
                    var idempotencyContext = request is IHttpIdempotentCommand idempotentCommand
                        ? idempotentCommand.IdempotencyContext
                        : null;
                    object? response = null;
                    var idempotencyRequest = (IdempotencyRequest?)null;
                    var shouldStoreOutcome = false;

                    if (idempotencyContext is not null)
                    {
                        var idempotencyScope = transaction.Idempotency
                            ?? throw new InvalidOperationException(
                                $"Transaction scope for '{typeof(TRequest).FullName}' does not support HTTP idempotency.");
                        var protector = _idempotencyProtector
                            ?? throw new InvalidOperationException("HTTP idempotency protection is not registered.");
                        idempotencyRequest = protector.CreateRequest(request, idempotencyContext);
                        await idempotencyScope.AcquireLockAsync(idempotencyRequest, cancellationToken)
                            .ConfigureAwait(false);
                        var lookup = await idempotencyScope.AcquireAsync(idempotencyRequest, cancellationToken)
                            .ConfigureAwait(false);

                        if (lookup.Exists)
                        {
                            if (!lookup.FingerprintMatches)
                            {
                                response = protector.CreateKeyReuseConflict(typeof(TResponse));
                            }
                            else if (lookup.Expired)
                            {
                                response = ResultResponseFactory.CreateFailure<TResponse>(
                                    ApplicationError.Conflict("IDENTITY_IDEMPOTENCY_OUTCOME_UNAVAILABLE",
                                        IdentityReplayMessages.OutcomeUnavailable));
                            }
                            else
                            {
                                try
                                {
                                    response = protector.RestoreSuccess(lookup.EncryptedOutcome!.Value.ToArray(), typeof(TResponse));
                                    var replayError = await idempotencyScope.ValidateReplayAsync(request, response, cancellationToken)
                                        .ConfigureAwait(false);
                                    if (replayError is not null)
                                    {
                                        response = ResultResponseFactory.CreateFailure<TResponse>(replayError);
                                    }
                                }
                                catch (CryptographicException)
                                {
                                    response = ResultResponseFactory.CreateFailure<TResponse>(ApplicationError.Conflict(
                                        "IDENTITY_IDEMPOTENCY_OUTCOME_UNAVAILABLE", IdentityReplayMessages.OutcomeUnavailable));
                                }
                                catch (JsonException)
                                {
                                    response = ResultResponseFactory.CreateFailure<TResponse>(ApplicationError.Conflict(
                                        "IDENTITY_IDEMPOTENCY_OUTCOME_UNAVAILABLE", IdentityReplayMessages.OutcomeUnavailable));
                                }
                            }
                        }
                        else
                        {
                            shouldStoreOutcome = true;
                        }
                    }

                    if (response is null)
                    {
                        response = await next(cancellationToken).ConfigureAwait(false);
                    }

                    if (response is Modules.SharedKernel.Application.IResult result && !result.IsSuccess)
                    {
                        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);

                        return (TResponse)response!;
                    }

                    if (response is not Modules.SharedKernel.Application.IResult)
                    {
                        throw new InvalidOperationException(
                            $"Idempotent command '{typeof(TRequest).FullName}' must return a Result.");
                    }

                    if (shouldStoreOutcome)
                    {
                        await transaction.Idempotency!.StoreAsync(
                                idempotencyRequest!,
                                _idempotencyProtector!.ProtectSuccess(response!),
                                DateTimeOffset.UtcNow.AddHours(24),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    committed = true;

                    await _postCommitDispatcher.ExecuteAsync(
                        CancellationToken.None).ConfigureAwait(false);

                    return (TResponse)response!;
                }
                catch
                {
                    if (!committed)
                    {
                        await transaction.RollbackAsync(CancellationToken.None)
                            .ConfigureAwait(false);
                    }

                    throw;
                }
            }
        }

        private static bool IsCommand(TRequest request)
        {
            var requestType = request.GetType();

            if (typeof(ICommand).IsAssignableFrom(requestType))
            {
                return true;
            }

            return requestType
                .GetInterfaces()
                .Any(interfaceType =>
                    interfaceType.IsGenericType &&
                    interfaceType.GetGenericTypeDefinition() == typeof(ICommand<>));
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
