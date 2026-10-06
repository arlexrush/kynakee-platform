using Kynakee.Api.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Contracts;

namespace Kynakee.Api.Infrastructure.Persistence
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public sealed class ModuleTransactionManager : ITransactionManager
    {
        private readonly IReadOnlyCollection<IModuleTransactionParticipant> _participants;

        public ModuleTransactionManager(IEnumerable<IModuleTransactionParticipant> participants)
        {
            _participants = participants.ToArray();
        }

        /// <summary>
        /// Inicia un alcance de transacción para el participante que corresponde al tipo de petición proporcionado.
        /// </summary>
        /// <remarks>Lanza ArgumentNullException si requestType es null.</remarks>
        /// <param name="requestType">Tipo de petición usado para resolver el participante; no puede ser null.</param>
        /// <param name="cancellationToken">Token para cancelar la operación asíncrona.</param>
        /// <returns>Tarea que produce un IModuleTransactionScope que representa el alcance de la transacción iniciado.</returns>
        public Task<IModuleTransactionScope> BeginAsync(
            Type requestType,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(requestType);

            var participant = ResolveParticipant(requestType); // Resuelve el participante correspondiente al tipo de petición

            return participant.BeginTransactionAsync(cancellationToken);
        }

        private IModuleTransactionParticipant ResolveParticipant(
            Type requestType)
        {
            var matches = _participants
                .Where(participant => participant.CanHandle(requestType))
                .ToArray();

            return matches.Length switch
            {
                1 => matches[0],
                0 => throw new InvalidOperationException(
                    $"No transaction participant is registered for request type '{requestType.FullName}'."),
                _ => throw new InvalidOperationException(
                    $"Multiple transaction participants are registered for request type '{requestType.FullName}'.")
            };
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
