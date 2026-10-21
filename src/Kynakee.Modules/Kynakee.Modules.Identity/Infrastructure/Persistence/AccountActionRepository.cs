using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence;

internal sealed class AccountActionRepository(IdentityDbContext context) : IAccountActionRepository
{
    public async Task LockEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(MailDelivery.AccountEmailMessages.Get("TransactionRequired"));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({digest}, 17))", cancellationToken).ConfigureAwait(false);
    }

    public Task<AccountActionToken?> FindAsync(string digest, CancellationToken cancellationToken) =>
        context.AccountActionTokens.IgnoreQueryFilters().SingleOrDefaultAsync(token => token.TokenHash == digest && !token.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<AccountActionToken>> ListAsync(Guid tenantId, Guid userId, AccountActionPurpose purpose, CancellationToken cancellationToken) =>
        await context.AccountActionTokens.IgnoreQueryFilters().Where(token => token.TenantId == tenantId && token.UserId == userId &&
            token.Purpose == purpose && !token.IsDeleted).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(AccountActionToken token) => context.AccountActionTokens.Add(token);

    public Task ReloadAsync(AccountActionToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        return context.Entry(token).ReloadAsync(cancellationToken);
    }

    public Task ReloadUserAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return context.Entry(user).ReloadAsync(cancellationToken);
    }
}
