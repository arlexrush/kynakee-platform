using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Domain.Events;

public sealed record TenantCreatedDomainEvent(Guid TenantId, string Name) : DomainEvent;