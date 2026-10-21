using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.SharedKernel.Contracts;

public interface IIdempotencyTransactionScope
{
    Task AcquireLockAsync(
        IdempotencyRequest request,
        CancellationToken cancellationToken);

    Task<IdempotencyLookupResult> AcquireAsync(
        IdempotencyRequest request,
        CancellationToken cancellationToken);

    Task StoreAsync(
        IdempotencyRequest request,
        byte[] encryptedOutcome,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    /// <summary>Revalida el estado de seguridad actual antes de devolver un resultado almacenado.</summary>
    Task<ApplicationError?> ValidateReplayAsync(object request, object response, CancellationToken cancellationToken);
}

public interface IHttpIdempotencyProtector
{
    IdempotencyRequest CreateRequest(object request, HttpIdempotencyContext context);

    byte[] ProtectSuccess(object response);

    object RestoreSuccess(byte[] protectedOutcome, Type responseType);

    object CreateKeyReuseConflict(Type responseType);
}

public sealed record IdempotencyRequest(
    string KeyHash,
    string Fingerprint,
    Guid TenantId,
    Guid CallerId,
    string Operation);

public sealed record IdempotencyLookupResult(
    bool Exists,
    bool FingerprintMatches,
    ReadOnlyMemory<byte>? EncryptedOutcome,
    bool Expired = false);

public interface IHttpIdempotentCommand
{
    HttpIdempotencyContext? IdempotencyContext { get; }
}

public sealed record HttpIdempotencyContext(
    string Operation,
    string Key,
    Guid TenantId,
    Guid CallerId);
