using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Application.Commands.ReserveCredits;

public sealed record ReserveCreditsCommand(
    Guid TenantId,
    decimal Amount,
    Guid OperationId,
    DateTime ExpiresAt) : ICommand<CreditReservation>;