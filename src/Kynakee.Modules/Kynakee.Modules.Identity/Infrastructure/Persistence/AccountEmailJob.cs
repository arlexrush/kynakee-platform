using Kynakee.Modules.SharedKernel.Domain;
using System.Security.Cryptography;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence;

/// <summary>Durable encrypted handoff for an account action email, never an invitation.</summary>
public sealed class AccountEmailJob : BaseEntity<Guid>
{
    private AccountEmailJob() { }

    private AccountEmailJob(Guid tenantId, Guid tokenId, byte[] payload, DateTimeOffset expiresAt, DateTimeOffset now)
        : base(Guid.NewGuid(), tenantId)
    {
        TokenId = tokenId;
        EncryptedPayload = payload.ToArray();
        ExpiresAt = expiresAt;
        NextAttemptAt = now;
    }

    public Guid TokenId { get; private set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819", Justification = "EF stores encrypted bytes; plaintext is never persisted.")]
    public byte[] EncryptedPayload { get; private set; } = [];
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? FailureCode { get; private set; }

    /// <summary>Copies an already encrypted payload into an uncommitted job.</summary>
    public static AccountEmailJob Create(Guid tenantId, Guid tokenId, byte[] payload, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(tokenId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, now);
        return new AccountEmailJob(tenantId, tokenId, payload, expiresAt, now);
    }

    /// <summary>Erases encrypted material when delivery is terminal.</summary>
    public void Complete(DateTimeOffset now, string? failureCode = null)
    {
        CompletedAt = now;
        FailureCode = failureCode;
        CryptographicOperations.ZeroMemory(EncryptedPayload);
        EncryptedPayload = [];
        RegisterUpdate();
    }

    /// <summary>Schedules bounded retries without persisting provider messages or credentials.</summary>
    public void Retry(DateTimeOffset now, string failureCode)
    {
        if (CompletedAt is not null) return;
        Attempts++;
        FailureCode = failureCode;
        NextAttemptAt = now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(Attempts - 1, 7))));
        if (Attempts >= 8 || NextAttemptAt >= ExpiresAt) Complete(now, failureCode);
        RegisterUpdate();
    }
}
