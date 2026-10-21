using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.Aggregates;

namespace Kynakee.Modules.Identity.Application.Abstractions;

/// <summary>Coordinates account credential issuance and consumption inside the Identity transaction.</summary>
public interface IAccountActionRepository
{
    /// <summary>Serializes actions for one normalized email without persisting the email as a lock key.</summary>
    Task LockEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    /// <summary>Finds a digest without consuming it; the caller subsequently locks and reloads account state.</summary>
    Task<AccountActionToken?> FindAsync(string digest, CancellationToken cancellationToken);
    /// <summary>Loads all nondeleted actions of one purpose for invalidation and cooldown checks.</summary>
    Task<IReadOnlyList<AccountActionToken>> ListAsync(Guid tenantId, Guid userId, AccountActionPurpose purpose, CancellationToken cancellationToken);
    /// <summary>Stages a new token in the caller transaction.</summary>
    void Add(AccountActionToken token);
    /// <summary>Reloads token state after serialization to avoid stale tracked lifecycle values.</summary>
    Task ReloadAsync(AccountActionToken token, CancellationToken cancellationToken);
    /// <summary>Reloads persisted user state after acquiring the account serialization lock.</summary>
    Task ReloadUserAsync(User user, CancellationToken cancellationToken);
}
