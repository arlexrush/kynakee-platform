using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public sealed class ModuleDomainEventCollector : IDomainEventCollector
    {
        private readonly IReadOnlyCollection<IModuleTransactionParticipant>
            _participants;

        public ModuleDomainEventCollector(
            IEnumerable<IModuleTransactionParticipant> participants)
        {
            _participants = participants.ToArray();
        }

        public IReadOnlyCollection<IDomainEvent> Collect(
            Type requestType)
        {
            ArgumentNullException.ThrowIfNull(requestType);

            var participant = FindParticipant(requestType);

            return participant?.CollectDomainEvents()
                ?? Array.Empty<IDomainEvent>();
        }

        public void Clear(
            Type requestType,
            IReadOnlyCollection<IDomainEvent> domainEvents)
        {
            ArgumentNullException.ThrowIfNull(requestType);
            ArgumentNullException.ThrowIfNull(domainEvents);

            var participant = FindParticipant(requestType);
            participant?.ClearDomainEvents(domainEvents);
        }

        private IModuleTransactionParticipant? FindParticipant(
            Type requestType)
        {
            ArgumentNullException.ThrowIfNull(requestType);

            var matches = _participants
                .Where(participant => participant.CanHandle(requestType))
                .ToArray();

            return matches.Length switch
            {
                0 => null,
                1 => matches[0],
                _ => throw new InvalidOperationException(
                    $"Multiple transaction participants are registered for request type '{requestType.FullName}'.")
            };
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
