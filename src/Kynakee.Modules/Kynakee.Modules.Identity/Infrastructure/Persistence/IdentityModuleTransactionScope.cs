using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Application.Commands.InviteUser;
using Kynakee.Modules.Identity.Application.Commands.AccountActions;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Storage;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence;

internal sealed class IdentityModuleTransactionScope : IModuleTransactionScope, IIdempotencyTransactionScope
{
    private readonly IdentityDbContext _dbContext;
    private readonly IDbContextTransaction _transaction;
    private readonly TimeProvider _timeProvider;
    private bool _completed;

    public IIdempotencyTransactionScope? Idempotency => this;

    internal IdentityModuleTransactionScope(
        IdentityDbContext dbContext,
        IDbContextTransaction transaction,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(transaction);
        _dbContext = dbContext;
        _transaction = transaction;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IdempotencyLookupResult> AcquireAsync(
        IdempotencyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var existing = await _dbContext.IdempotencyRecords
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                record => record.KeyHash == request.KeyHash,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return new IdempotencyLookupResult(false, true, null);
        }

        if (existing.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            return new IdempotencyLookupResult(true, existing.Fingerprint == request.Fingerprint, null, true);
        }

        return new IdempotencyLookupResult(
            true,
            existing.Fingerprint == request.Fingerprint,
            existing.GetEncryptedOutcome());
    }

    public async Task AcquireLockAsync(
        IdempotencyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({request.KeyHash}, 0))",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task StoreAsync(
        IdempotencyRequest request,
        byte[] encryptedOutcome,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(encryptedOutcome);

        var existing = await _dbContext.IdempotencyRecords
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                record => record.KeyHash == request.KeyHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            _dbContext.IdempotencyRecords.Add(IdentityIdempotencyRecord.Create(
                request.TenantId,
                request.KeyHash,
                request.Fingerprint,
                encryptedOutcome,
                expiresAt));
            return;
        }

        throw new InvalidOperationException(IdentityReplayMessages.OutcomeUnavailable);
    }

    public async Task<ApplicationError?> ValidateReplayAsync(object request, object response, CancellationToken cancellationToken)
    {
        if (request is RegisterTenantOwnerCommand or ResendVerificationCommand or RequestPasswordResetCommand
            && response is Result<AccountActionAcknowledgement> { IsSuccess: true, Value: not null })
            return null;

        if (response is Result<IdentitySessionResponse> sessionResult && sessionResult.Value is { } session)
        {
            var tenantId = session.Tenant.Id;
            var userId = session.User.Id;
            var now = _timeProvider.GetUtcNow();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session.RefreshToken)));
            var active = await (from user in _dbContext.Users.IgnoreQueryFilters()
                                join tenant in _dbContext.Tenants.IgnoreQueryFilters() on user.TenantId equals tenant.Id
                                join member in _dbContext.TenantUsers.IgnoreQueryFilters() on user.Id equals member.UserId
                                join token in _dbContext.RefreshTokens.IgnoreQueryFilters() on user.Id equals token.UserId
                                where user.Id == userId && user.TenantId == tenantId && !user.IsDeleted && user.Status == UserStatus.Active
                                && !tenant.IsDeleted && tenant.Status == TenantStatus.Active
                                && member.TenantId == tenantId && !member.IsDeleted && member.Role == session.User.Role
                                && token.TenantId == tenantId && token.TokenHash == hash && !token.IsDeleted
                                && token.RevokedAt == null && token.ReplacedByTokenId == null && token.ExpiresAt > now
                                select user.Id).AnyAsync(cancellationToken).ConfigureAwait(false);
            if (!active)
            {
                return ApplicationError.Unauthorized("IDENTITY_REPLAY_SESSION_UNAVAILABLE", IdentityReplayMessages.SessionUnavailable);
            }
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
            return jwt.ValidTo <= now.UtcDateTime
                ? ApplicationError.Conflict("IDENTITY_IDEMPOTENCY_OUTCOME_UNAVAILABLE", IdentityReplayMessages.OutcomeUnavailable)
                : null;
        }

        if (request is InviteUserCommand invite && response is Result<InvitationCreatedResponse> invitationResult
            && invitationResult.Value is { } invitation)
        {
            var allowed = await (from user in _dbContext.Users.IgnoreQueryFilters()
                                 join tenant in _dbContext.Tenants.IgnoreQueryFilters() on user.TenantId equals tenant.Id
                                 join member in _dbContext.TenantUsers.IgnoreQueryFilters() on user.Id equals member.UserId
                                 where user.Id == invite.InviterUserId && user.TenantId == invite.TenantId
                                 && !user.IsDeleted && user.Status == UserStatus.Active && !tenant.IsDeleted && tenant.Status == TenantStatus.Active
                                 && member.TenantId == invite.TenantId && !member.IsDeleted
                                 && (member.Role == UserRole.Owner || member.Role == UserRole.Admin)
                                 select user.Id).AnyAsync(cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                return ApplicationError.Unauthorized("IDENTITY_INVITATION_FORBIDDEN", IdentityReplayMessages.InvitationForbidden);
            }
            var now = _timeProvider.GetUtcNow();
            var usable = await _dbContext.UserInvitations.IgnoreQueryFilters().AnyAsync(
                item => item.Id == invitation.InvitationId && item.TenantId == invite.TenantId && !item.IsDeleted
                    && item.AcceptedAt == null && item.RevokedAt == null && item.ExpiresAt > now, cancellationToken).ConfigureAwait(false);
            return usable ? null : ApplicationError.Conflict("IDENTITY_REPLAY_INVITATION_UNAVAILABLE", IdentityReplayMessages.InvitationUnavailable);
        }

        return ApplicationError.Conflict("IDENTITY_IDEMPOTENCY_OUTCOME_UNAVAILABLE", IdentityReplayMessages.OutcomeUnavailable);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        await _transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
        _completed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken)
            .ConfigureAwait(false);
        _dbContext.ChangeTracker.Clear();
        _completed = true;
    }

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}