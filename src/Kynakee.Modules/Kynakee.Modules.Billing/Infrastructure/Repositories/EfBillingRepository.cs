using Kynakee.Modules.Billing.Application.Abstractions;
using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Billing.Infrastructure.Repositories;

public sealed class EfBillingRepository : IBillingRepository
{
    private readonly BillingDbContext _dbContext;

    public EfBillingRepository(BillingDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<CreditAccount?> GetAccountForUpdateAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        _dbContext.CreditAccounts
            .Include(account => account.CreditLots)
            .Include(account => account.Transactions)
                .ThenInclude(transaction => transaction.Allocations)
            .SingleOrDefaultAsync(account => account.TenantId == tenantId, cancellationToken);

    public Task<Subscription?> GetSubscriptionForUpdateAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        _dbContext.Subscriptions
            .SingleOrDefaultAsync(subscription => subscription.TenantId == tenantId, cancellationToken);

    public async Task<CreditBalanceProjection?> GetBalanceAsync(
        Guid tenantId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var account = await _dbContext.CreditAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new
            {
                item.PlanId,
                AvailableCredits = item.CreditLots
                    .Where(lot => lot.ExpiresAt > utcNow)
                    .Sum(lot => (decimal?)lot.AvailableCredits) ?? 0m,
                ReservedCredits = item.CreditLots.Sum(lot => (decimal?)lot.ReservedCredits) ?? 0m,
                NextRenewalDate = _dbContext.Subscriptions
                    .Where(subscription => subscription.TenantId == item.TenantId)
                    .Select(subscription => (DateTime?)subscription.CurrentPeriodEnd)
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        return new CreditBalanceProjection(
            account.AvailableCredits,
            account.ReservedCredits,
            account.PlanId.Value,
            account.NextRenewalDate);
    }

    public async Task<CreditTransactionPageProjection> GetTransactionsAsync(
        Guid tenantId,
        CreditTransactionType? type,
        DateTime? from,
        DateTime? endDate,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.CreditTransactions
            .AsNoTracking()
            .Where(transaction => transaction.TenantId == tenantId);
        if (type.HasValue)
        {
            query = query.Where(transaction => transaction.Type == type.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(transaction => transaction.CreatedAt >= from.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(transaction => transaction.CreatedAt <= endDate.Value);
        }

        if (Guid.TryParseExact(cursor, "N", out var cursorId))
        {
            var cursorCreatedAt = await query
                .Where(transaction => transaction.Id == cursorId)
                .Select(transaction => (DateTime?)transaction.CreatedAt)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (cursorCreatedAt.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.CreatedAt < cursorCreatedAt.Value ||
                    (transaction.CreatedAt == cursorCreatedAt.Value && transaction.Id.CompareTo(cursorId) < 0));
            }
        }

        var rows = await query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Take(limit + 1)
            .Select(transaction => new CreditTransactionProjection(
                transaction.Id,
                transaction.OperationId,
                transaction.Type,
                transaction.Amount,
                transaction.Source,
                transaction.CreatedAt,
                transaction.ExpiresAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasMore = rows.Count > limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new CreditTransactionPageProjection(
            rows,
            hasMore && rows.Count > 0 ? rows[^1].Id.ToString("N") : null);
    }

    public void AddAccount(CreditAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        _dbContext.CreditAccounts.Add(account);
    }

    public void AddSubscription(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        _dbContext.Subscriptions.Add(subscription);
    }
}