using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Application.Queries.GetCreditTransactions;

public sealed record GetCreditTransactionsQuery(
    Guid TenantId,
    CreditTransactionType? Type,
    DateTime? From,
    DateTime? EndDate,
    string? Cursor,
    int Limit = 50) : IQuery<CreditTransactionHistoryDto>;