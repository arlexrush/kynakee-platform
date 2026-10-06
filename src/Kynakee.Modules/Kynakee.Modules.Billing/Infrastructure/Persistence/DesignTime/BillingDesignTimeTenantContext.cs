using Kynakee.Modules.SharedKernel.Contracts;

namespace Kynakee.Modules.Billing.Infrastructure.Persistence.DesignTime;

internal sealed class BillingDesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;

    public Guid UserId => Guid.Empty;

    public bool IsAuthenticated => false;
}