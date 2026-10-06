using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Domain.Entities;

public sealed class UserInvitation : BaseEntity<Guid>
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private UserInvitation()
    {
    }

    private UserInvitation(
        Guid id,
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset issuedAt,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = issuedAt.Add(Lifetime);
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static Result<UserInvitation> Create(
        Guid tenantId,
        Guid userId,
        string? tokenHash,
        DateTimeOffset issuedAt,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return ResultFactory.Failure<UserInvitation>(
                ApplicationError.Validation("IDENTITY_INVITATION_OWNER_REQUIRED", "Invitation tenant and user are required."));
        }

        if (!IsSha256Hash(tokenHash))
        {
            return ResultFactory.Failure<UserInvitation>(
                ApplicationError.Validation("IDENTITY_INVITATION_HASH_INVALID", "Invitation token must be stored as a SHA-256 hash."));
        }

        return ResultFactory.Success(new UserInvitation(
            Guid.NewGuid(),
            tenantId,
            userId,
            tokenHash!,
            issuedAt.ToUniversalTime(),
            createdBy));
    }

    public bool IsUsableAt(DateTimeOffset now) =>
        !IsDeleted && AcceptedAt is null && RevokedAt is null && now.ToUniversalTime() < ExpiresAt;

    public Result Accept(DateTimeOffset acceptedAt, Guid? updatedBy = null)
    {
        if (IsDeleted || AcceptedAt is not null || RevokedAt is not null)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_INVITATION_UNAVAILABLE", "Invitation is no longer available."));
        }

        var normalizedAcceptedAt = acceptedAt.ToUniversalTime();
        if (normalizedAcceptedAt >= ExpiresAt)
        {
            return ResultFactory.Failure(
                ApplicationError.Unauthorized("IDENTITY_INVITATION_EXPIRED", "Invitation has expired."));
        }

        AcceptedAt = normalizedAcceptedAt;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result Revoke(DateTimeOffset revokedAt, Guid? updatedBy = null)
    {
        if (IsDeleted || AcceptedAt is not null || RevokedAt is not null)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_INVITATION_UNAVAILABLE", "Invitation is no longer available."));
        }

        RevokedAt = revokedAt.ToUniversalTime();
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    private static bool IsSha256Hash(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}