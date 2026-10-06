using Kynakee.Api.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;


namespace Kynakee.Api.Application.Behaviors
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IPostCommitActionDispatcher _postCommitDispatcher;

        public TransactionBehavior(ITransactionManager transactionManager, IPostCommitActionDispatcher postCommitDispatcher)
        {
            _transactionManager = transactionManager;
            _postCommitDispatcher = postCommitDispatcher;
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
                    var response = await next(cancellationToken).ConfigureAwait(false);

                    if (response is Modules.SharedKernel.Application.IResult result && !result.IsSuccess)
                    {
                        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);

                        return response;
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    committed = true;

                    await _postCommitDispatcher.ExecuteAsync(
                        CancellationToken.None).ConfigureAwait(false);

                    return response;
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
