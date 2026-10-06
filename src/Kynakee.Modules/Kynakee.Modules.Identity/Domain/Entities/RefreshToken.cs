using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Domain.Entities;

public sealed class RefreshToken : BaseEntity<Guid>
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static Result<RefreshToken> Create(
        Guid tenantId,
        Guid userId,
        string? tokenHash,
        DateTimeOffset createdAt,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return ResultFactory.Failure<RefreshToken>(
                ApplicationError.Validation("IDENTITY_REFRESH_TOKEN_OWNER_REQUIRED", "Refresh token tenant and user are required."));
        }

        if (!IsSha256Hash(tokenHash))
        {
            return ResultFactory.Failure<RefreshToken>(
                ApplicationError.Validation("IDENTITY_REFRESH_TOKEN_HASH_INVALID", "Refresh token must be stored as a SHA-256 hash."));
        }

        var normalizedCreatedAt = createdAt.ToUniversalTime();
        var token = new RefreshToken(
            Guid.NewGuid(),
            tenantId,
            userId,
            tokenHash!,
            normalizedCreatedAt.Add(Lifetime),
            createdBy);
        return ResultFactory.Success(token);
    }

    public bool IsUsableAt(DateTimeOffset now) =>
        !IsDeleted && RevokedAt is null && now.ToUniversalTime() < ExpiresAt;

    public Result Revoke(
        DateTimeOffset revokedAt,
        Guid? replacedByTokenId = null,
        Guid? updatedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_REFRESH_TOKEN_DELETED", "A deleted refresh token cannot be changed."));
        }

        if (RevokedAt is not null)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_REFRESH_TOKEN_REVOKED", "Refresh token has already been revoked."));
        }

        if (replacedByTokenId == Id || replacedByTokenId == Guid.Empty)
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_REFRESH_TOKEN_REPLACEMENT_INVALID", "Replacement token id is invalid."));
        }

        RevokedAt = revokedAt.ToUniversalTime();
        ReplacedByTokenId = replacedByTokenId;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    private static bool IsSha256Hash(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}