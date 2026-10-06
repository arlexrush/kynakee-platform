using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Domain.Events;

public sealed record UserInvitedDomainEvent(
    Guid UserId,
    Guid TenantId,
    string Email) : DomainEvent;

public sealed record UserRegisteredDomainEvent(
    Guid UserId,
    Guid TenantId,
    string Email) : DomainEvent;
