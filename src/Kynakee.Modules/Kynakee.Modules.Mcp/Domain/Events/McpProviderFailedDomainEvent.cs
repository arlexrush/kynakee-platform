using Kynakee.Modules.SharedKernel.Domain;
using Kynakee.Modules.Mcp.Domain.ValueObjects;

namespace Kynakee.Modules.Mcp.Domain.Events;

public sealed record McpProviderFailedDomainEvent(
    McpProviderId ProviderId,
    Guid TenantId,
    int ConsecutiveFailures) : DomainEvent;
