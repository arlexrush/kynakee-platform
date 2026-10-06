using Kynakee.Modules.SharedKernel.Contracts;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.DesignTime;

internal sealed class IdentityDesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;

    public Guid UserId => Guid.Empty;

    public bool IsAuthenticated => false;
}
