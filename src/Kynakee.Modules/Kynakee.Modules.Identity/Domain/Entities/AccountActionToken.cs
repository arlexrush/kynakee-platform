using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;
using System.Globalization;
using System.Resources;

namespace Kynakee.Modules.Identity.Domain.Entities;

/// <summary>Identifies the only account operation a token may authorize.</summary>
public enum AccountActionPurpose
{
    VerifyEmail,
    ResetPassword
}

/// <summary>Stores a digest only; consuming a token and its account effect must share a transaction.</summary>
public sealed class AccountActionToken : BaseEntity<Guid>
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Identity.Domain.AccountActionResources", typeof(AccountActionToken).Assembly);

    private AccountActionToken() { }

    private AccountActionToken(Guid tenantId, Guid userId, string tokenHash, AccountActionPurpose purpose,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt) : base(Guid.NewGuid(), tenantId, userId)
    {
        UserId = userId;
        TokenHash = tokenHash;
        Purpose = purpose;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public AccountActionPurpose Purpose { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Creates a purpose-bound token record with an explicit expiry supplied by the application policy.</summary>
    public static Result<AccountActionToken> Create(Guid tenantId, Guid userId, string? tokenHash,
        AccountActionPurpose purpose, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        if (tokenHash is null)
            return ResultFactory.Failure<AccountActionToken>(ApplicationError.Validation(
                "IDENTITY_ACCOUNT_TOKEN_INVALID", Message("Invalid")));

        if (tenantId == Guid.Empty || userId == Guid.Empty || !Enum.IsDefined(purpose) ||
            tokenHash is not { Length: 64 } || !tokenHash.All(Uri.IsHexDigit) || expiresAt <= issuedAt)
        {
            return ResultFactory.Failure<AccountActionToken>(ApplicationError.Validation(
                "IDENTITY_ACCOUNT_TOKEN_INVALID", Message("Invalid")));
        }

        return ResultFactory.Success(new AccountActionToken(tenantId, userId, tokenHash.ToUpperInvariant(),
            purpose, issuedAt.ToUniversalTime(), expiresAt.ToUniversalTime()));
    }

    /// <summary>Checks time, purpose and lifecycle without revealing which condition rejected a token.</summary>
    public bool IsUsableAt(AccountActionPurpose purpose, DateTimeOffset now) =>
        !IsDeleted && Purpose == purpose && ConsumedAt is null && RevokedAt is null && now >= IssuedAt && now < ExpiresAt;

    /// <summary>Consumes once; the repository must serialize consumption with account mutation.</summary>
    public Result Consume(AccountActionPurpose purpose, DateTimeOffset now)
    {
        if (!IsUsableAt(purpose, now))
            return ResultFactory.Failure(ApplicationError.Unauthorized("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", Message("Unavailable")));

        ConsumedAt = now.ToUniversalTime();
        RegisterUpdate(UserId);
        return ResultFactory.Ok();
    }

    /// <summary>Revokes an unused token, without changing a previously terminal state.</summary>
    public Result Revoke(DateTimeOffset now)
    {
        if (IsDeleted || ConsumedAt is not null || RevokedAt is not null || now < IssuedAt)
            return ResultFactory.Failure(ApplicationError.Conflict("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", Message("Unavailable")));

        RevokedAt = now.ToUniversalTime();
        RegisterUpdate(UserId);
        return ResultFactory.Ok();
    }

    private static string Message(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
