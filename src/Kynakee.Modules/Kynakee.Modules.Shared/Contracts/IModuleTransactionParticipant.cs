using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.SharedKernel.Contracts
{
    /// <summary>
    /// Representa un participante en la coordinación de una transacción de módulo, capaz de indicar si puede manejar un
    /// tipo de petición, iniciar un ámbito transaccional y gestionar eventos de dominio.
    /// </summary>
    /// <remarks>Las implementaciones deben respetar el CancellationToken en BeginTransactionAsync y devolver
    /// un IModuleTransactionScope que controle el ciclo de vida de la transacción. CollectDomainEvents debe reunir los
    /// eventos de dominio pendientes para su publicación (por ejemplo, mediante un outbox) y ClearDomainEvents debe
    /// eliminar los eventos ya procesados. Está pensado para usarse por un coordinador que orquesta varios
    /// participantes; la persistencia y las transiciones de estado deben ocurrir dentro del ámbito transaccional
    /// proporcionado por el participante. La seguridad de subprocesos y la idempotencia dependen de la implementación
    /// concreta.</remarks>
    public interface IModuleTransactionParticipant
    {
        /// <summary>
        /// Determina si el manejador puede procesar el tipo de solicitud especificado.
        /// </summary>
        /// <remarks>La evaluación puede considerar herencia, interfaces y parámetros genéricos según la
        /// implementación.</remarks>
        /// <param name="requestType">Tipo de la solicitud a evaluar. Si es null o no compatible, se considera no manejable.</param>
        /// <returns>true si el manejador puede procesar el tipo de solicitud; en caso contrario, false.</returns>
        bool CanHandle(Type requestType);

        /// <summary>
        /// Inicia una transacción a nivel de módulo y devuelve un ámbito transaccional que controla su duración y
        /// confirmación.
        /// </summary>
        /// <remarks>El ámbito devuelto debe completarse o descartarse para confirmar o revertir la
        /// transacción. Las implementaciones pueden aplicar políticas de aislamiento y participar en patrones de
        /// integración (por ejemplo, Outbox).</remarks>
        /// <param name="cancellationToken">Token de cancelación para abortar la operación asincrónica de inicio de la transacción.</param>
        /// <returns>Una tarea que produce un IModuleTransactionScope para controlar el ámbito de la transacción.</returns>
        Task<IModuleTransactionScope> BeginTransactionAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// Obtiene una instantánea de los eventos de dominio acumulados por la entidad.
        /// </summary>
        /// <remarks>La colección es una copia inmutable del estado interno; no modifica ni elimina los
        /// eventos almacenados en la entidad.</remarks>
        /// <returns>Colección de solo lectura de IDomainEvent que contiene los eventos de dominio pendientes de publicación o
        /// procesamiento.</returns>
        IReadOnlyCollection<IDomainEvent> CollectDomainEvents();

        /// <summary>
        /// Elimina los eventos de dominio especificados del almacenamiento interno de eventos pendientes.
        /// </summary>
        /// <remarks>Operación idempotente; pasar una instantánea de los eventos actuales para evitar
        /// condiciones de carrera.</remarks>
        /// <param name="domainEvents">Colección inmutable de eventos de dominio que se deben eliminar.</param>
        void ClearDomainEvents(
            IReadOnlyCollection<IDomainEvent> domainEvents);
    }
}
