using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.Events
{
    public sealed record ProjectCreatedEvent(
    ProjectId ProjectId,
    Guid TenantId,
    GeoLocation Location,
    ProjectChannel Channel) : DomainEvent;

    public sealed record PhaseAdvancedEvent(
        ProjectId ProjectId,
        Guid TenantId,
        ProjectPhase PreviousPhase,
        ProjectPhase NewPhase) : DomainEvent;

    public sealed record WorkItemAddedEvent(
        ProjectId ProjectId,
        Guid TenantId,
        WorkItemId WorkItemId,
        CanonicalConceptId CanonicalConceptId) : DomainEvent;

    public sealed record WorkItemUpdatedEvent(
        ProjectId ProjectId,
        Guid TenantId,
        WorkItemId WorkItemId) : DomainEvent;

    public sealed record ValuationInvalidatedEvent(
        ProjectId ProjectId,
        Guid TenantId,
        string Reason) : DomainEvent;

    public sealed record ReviewApprovedEvent(
        ProjectId ProjectId,
        Guid TenantId,
        UserId ReviewerId,
        int ChangesCount) : DomainEvent;

    public sealed record OfferGeneratedEvent(
        ProjectId ProjectId,
        Guid TenantId,
        Money TotalAmount,
        int OfferNumber) : DomainEvent;

    public sealed record ProjectPausedEvent(
        ProjectId ProjectId,
        Guid TenantId,
        DateTime PausedAt,
        ProjectChannel Channel) : DomainEvent;

    public sealed record ProjectCancelledEvent(
        ProjectId ProjectId,
        Guid TenantId,
        string Reason) : DomainEvent;
}
