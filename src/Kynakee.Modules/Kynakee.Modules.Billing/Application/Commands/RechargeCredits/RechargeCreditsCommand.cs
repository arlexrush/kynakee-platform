using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Application.Commands.RechargeCredits;

public sealed record RechargeCreditsCommand(
    Guid TenantId,
    decimal Amount,
    CreditSource Source,
    DateTime PurchasedAt,
    DateTime ExpiresAt,
    Guid? CreatedBy = null) : ICommand<CreditLotId>;