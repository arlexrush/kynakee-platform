using Kynakee.Api.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Api.Application.Behaviors
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public sealed class DomainEventDispatchBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    {
        private readonly IDomainEventCollector _eventCollector;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IPostCommitActionDispatcher _postCommitDispatcher;

        public DomainEventDispatchBehavior(
            IDomainEventCollector eventCollector,
            IDomainEventDispatcher eventDispatcher,
            IPostCommitActionDispatcher postCommitDispatcher)
        {
            _eventCollector = eventCollector;
            _eventDispatcher = eventDispatcher;
            _postCommitDispatcher = postCommitDispatcher;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(next);

            var response = await next(cancellationToken)
                .ConfigureAwait(false);

            if (!IsSuccessful(response))
            {
                return response;
            }

            var requestType = typeof(TRequest);
            var domainEvents = _eventCollector.Collect(requestType);

            if (domainEvents.Count == 0)
            {
                return response;
            }

            _postCommitDispatcher.Enqueue(async postCommitCancellationToken =>
            {
                await _eventDispatcher.DispatchAsync(
                    domainEvents,
                    postCommitCancellationToken).ConfigureAwait(false);

                _eventCollector.Clear(requestType, domainEvents);
            });

            return response;
        }

        private static bool IsSuccessful(TResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            var property = response
                .GetType()
                .GetProperty("IsSuccess");

            return property?.PropertyType == typeof(bool) &&
                   (bool)property.GetValue(response)!;
        }
    }


#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
