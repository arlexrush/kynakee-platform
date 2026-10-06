using Kynakee.Modules.SharedKernel.Contracts;

namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public sealed class PostCommitActionDispatcher
    : IPostCommitActionDispatcher
    {
        private readonly List<Func<CancellationToken, Task>> _actions = [];

        public void Enqueue(
            Func<CancellationToken, Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            _actions.Add(action);
        }

        public async Task ExecuteAsync(
            CancellationToken cancellationToken)
        {
            var actions = _actions.ToArray();

            foreach (var action in actions)
            {
                await action(cancellationToken)
                    .ConfigureAwait(false);
            }

            _actions.Clear();
        }
    }


#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
