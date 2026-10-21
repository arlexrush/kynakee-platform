using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using System.Security.Cryptography;

namespace Kynakee.Modules.Identity.Infrastructure.MailDelivery;

/// <summary>Stages ciphertext in the same transaction as its account action.</summary>
public sealed class AccountEmailJobStore(IdentityDbContext context, AccountEmailPayloadProtector protector, TimeProvider timeProvider) : IAccountEmailJobStore
{
    /// <summary>Requires a tracked action in the current transaction; anonymous registration may have an empty tenant context.</summary>
    public void Enqueue(AccountActionToken action, string recipient, string plaintextToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);
        if (context.Database.CurrentTransaction is null ||
            (context.CurrentTenantId != Guid.Empty && context.CurrentTenantId != action.TenantId) ||
            context.Entry(action).State != Microsoft.EntityFrameworkCore.EntityState.Added)
            throw new InvalidOperationException(AccountEmailMessages.Get("TransactionRequired"));
        var encrypted = protector.Protect(action.TenantId, action.Id, new(recipient, plaintextToken, action.Purpose));
        try
        {
            context.AccountEmailJobs.Add(AccountEmailJob.Create(action.TenantId, action.Id, encrypted,
                action.ExpiresAt, timeProvider.GetUtcNow()));
        }
        finally { CryptographicOperations.ZeroMemory(encrypted); }
    }
}
