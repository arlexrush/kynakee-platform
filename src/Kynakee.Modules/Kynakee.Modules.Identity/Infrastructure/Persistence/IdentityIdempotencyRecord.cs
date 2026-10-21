using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityIdempotencyRecord : BaseEntity<Guid>
{
    private IdentityIdempotencyRecord()
    {
    }

    private IdentityIdempotencyRecord(
        Guid tenantId,
        string keyHash,
        string fingerprint,
        byte[] encryptedOutcome,
        DateTimeOffset expiresAt)
        : base(Guid.NewGuid(), tenantId)
    {
        KeyHash = keyHash;
        Fingerprint = fingerprint;
        _encryptedOutcome = encryptedOutcome.ToArray();
        ExpiresAt = expiresAt;
    }

    public string KeyHash { get; private set; } = string.Empty;

    public string Fingerprint { get; private set; } = string.Empty;

    private byte[] _encryptedOutcome = [];

    internal byte[] GetEncryptedOutcome() => _encryptedOutcome.ToArray();

    public DateTimeOffset ExpiresAt { get; private set; }

    public static IdentityIdempotencyRecord Create(
        Guid tenantId,
        string keyHash,
        string fingerprint,
        byte[] encryptedOutcome,
        DateTimeOffset expiresAt) =>
        new(tenantId, keyHash, fingerprint, encryptedOutcome, expiresAt);

    public void Replace(
        string fingerprint,
        byte[] encryptedOutcome,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(encryptedOutcome);
        Fingerprint = fingerprint;
        _encryptedOutcome = encryptedOutcome.ToArray();
        ExpiresAt = expiresAt;
        RegisterUpdate();
    }

}
