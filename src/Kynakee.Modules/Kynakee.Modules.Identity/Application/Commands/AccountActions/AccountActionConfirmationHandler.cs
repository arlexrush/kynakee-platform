using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Kynakee.Modules.Identity.Application.Commands.AccountActions;

internal sealed class AccountActionConfirmationHandler(IIdentityRepository identities, IAccountActionRepository actions,
    IIdentityTokenService tokens, UserManager<User> users, TimeProvider timeProvider)
    : IRequestHandler<VerifyEmailCommand, Result<AccountActionAcknowledgement>>,
      IRequestHandler<ResetPasswordCommand, Result<AccountActionAcknowledgement>>
{
    public Task<Result<AccountActionAcknowledgement>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ConfirmAsync(request.Token, null, AccountActionPurpose.VerifyEmail, cancellationToken);
    }

    public Task<Result<AccountActionAcknowledgement>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ConfirmAsync(request.Token, request.Password, AccountActionPurpose.ResetPassword, cancellationToken);
    }

    private async Task<Result<AccountActionAcknowledgement>> ConfirmAsync(string plaintext, string? password,
        AccountActionPurpose purpose, CancellationToken cancellationToken)
    {
        var action = await actions.FindAsync(tokens.HashToken(plaintext), cancellationToken).ConfigureAwait(false);
        if (action is null) return Unavailable();
        var user = await identities.GetUserForAuthenticationAsync(action.TenantId, action.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null) return Unavailable();
        await actions.LockEmailAsync(user.Email.Value, cancellationToken).ConfigureAwait(false);
        await actions.ReloadAsync(action, cancellationToken).ConfigureAwait(false);
        await actions.ReloadUserAsync(user, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var tenant = await identities.GetTenantForAuthenticationAsync(user.TenantId, cancellationToken).ConfigureAwait(false);
        var membership = await identities.GetTenantUserForAuthenticationAsync(user.TenantId, user.Id, cancellationToken).ConfigureAwait(false);
        if (!action.IsUsableAt(purpose, now) || tenant is null || tenant.Status != TenantStatus.Active || membership is null)
            return Unavailable();
        Result mutation;
        if (purpose == AccountActionPurpose.VerifyEmail)
        {
            mutation = user.ConfirmRegistration(user.Id);
        }
        else
        {
            if (password is null || user.Status != UserStatus.Active) return Unavailable();
            foreach (var validator in users.PasswordValidators)
            {
                var validation = await validator.ValidateAsync(users, user, password).ConfigureAwait(false);
                if (!validation.Succeeded)
                    return ResultFactory.Failure<AccountActionAcknowledgement>(ApplicationError.Validation(
                        "IDENTITY_PASSWORD_REJECTED", AccountActionMessages.Get("PasswordRejected")));
            }
            mutation = user.RecoverPassword(users.PasswordHasher.HashPassword(user, password));
            if (mutation.IsFailure) return ResultFactory.Failure<AccountActionAcknowledgement>(mutation.Error!);
            var refreshTokens = await identities.GetActiveRefreshTokensAsync(user.TenantId, user.Id, cancellationToken).ConfigureAwait(false);
            foreach (var refresh in refreshTokens)
            {
                var revoked = refresh.Revoke(now, updatedBy: user.Id);
                if (revoked.IsFailure) return ResultFactory.Failure<AccountActionAcknowledgement>(revoked.Error!);
            }
        }
        if (mutation.IsFailure) return ResultFactory.Failure<AccountActionAcknowledgement>(mutation.Error!);
        var consumed = action.Consume(purpose, now);
        if (consumed.IsFailure) return ResultFactory.Failure<AccountActionAcknowledgement>(consumed.Error!);
        var siblings = await actions.ListAsync(user.TenantId, user.Id, purpose, cancellationToken).ConfigureAwait(false);
        foreach (var sibling in siblings.Where(candidate => candidate.Id != action.Id && candidate.ConsumedAt is null && candidate.RevokedAt is null))
        {
            var revoked = sibling.Revoke(now);
            if (revoked.IsFailure) return ResultFactory.Failure<AccountActionAcknowledgement>(revoked.Error!);
        }
        return ResultFactory.Success(new AccountActionAcknowledgement(AccountActionMessages.Get("Completed")));
    }

    private static Result<AccountActionAcknowledgement> Unavailable() => ResultFactory.Failure<AccountActionAcknowledgement>(
        ApplicationError.Unauthorized("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", AccountActionMessages.Get("Unavailable")));
}
