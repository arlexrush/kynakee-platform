using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Application.Queries.GetCreditBalance;

public sealed record GetCreditBalanceQuery(Guid TenantId) : IQuery<CreditBalanceDto>;