namespace Kynakee.Modules.SharedKernel.Integration
{
    /// <summary>
    /// Base class for all integration events in the Kynakee platform.
    /// Implements IIntegrationEvent providing default values for Id and OccurredOn.
    ///
    /// Integration events are published via MassTransit Outbox (ADR-006)
    /// and consumed by other modules via RabbitMQ (ADR-005).
    ///
    /// Usage: Inherit from this class to create integration events.
    ///
    /// Naming convention: {Aggregate}{Fact}IntegrationEvent
    /// Examples:
    ///   - ProjectCreatedIntegrationEvent
    ///   - OfferGeneratedIntegrationEvent
    ///   - CreditDeductedIntegrationEvent
    ///   - BotMessageReceivedIntegrationEvent
    ///
    /// Rules:
    /// - All properties MUST be init-only (immutable after creation)
    /// - TenantId MUST always be set — never leave it as Guid.Empty
    /// - Keep events flat — avoid nested objects (serialization complexity)
    /// - Version integration events if breaking changes are needed
    /// </summary>
    /// <example>
    /// public sealed class OfferGeneratedIntegrationEvent : IntegrationEvent
    /// {
    ///     public Guid   ProjectId  { get; init; }
    ///     public Guid   ClientId   { get; init; }
    ///     public string OfferPdfUrl { get; init; } = string.Empty;
    ///     public decimal TotalAmount { get; init; }
    ///
    ///     public OfferGeneratedIntegrationEvent(
    ///         Guid tenantId,
    ///         Guid projectId,
    ///         Guid clientId,
    ///         string offerPdfUrl,
    ///         decimal totalAmount)
    ///     {
    ///         TenantId    = tenantId;
    ///         ProjectId   = projectId;
    ///         ClientId    = clientId;
    ///         OfferPdfUrl = offerPdfUrl;
    ///         TotalAmount = totalAmount;
    ///     }
    /// }
    /// </example>
    public abstract class IntegrationEvent : IIntegrationEvent
    {
        /// <inheritdoc />
        public Guid Id { get; } = Guid.NewGuid();

        /// <inheritdoc />
        public DateTime OccurredOn { get; } = DateTime.UtcNow;

        /// <inheritdoc />
        public Guid TenantId { get; protected init; }
    }
}
