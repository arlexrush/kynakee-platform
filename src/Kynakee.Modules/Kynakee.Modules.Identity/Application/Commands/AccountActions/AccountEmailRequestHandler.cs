using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.AccountActions;

internal sealed class AccountEmailRequestHandler(IIdentityRepository identities, IAccountActionRepository actions,
    AccountActionIssuer issuer) : IRequestHandler<ResendVerificationCommand, Result<AccountActionAcknowledgement>>,
    IRequestHandler<RequestPasswordResetCommand, Result<AccountActionAcknowledgement>>
{
    public Task<Result<AccountActionAcknowledgement>> Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RequestAsync(request.Email, AccountActionPurpose.VerifyEmail, cancellationToken);
    }

    public Task<Result<AccountActionAcknowledgement>> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RequestAsync(request.Email, AccountActionPurpose.ResetPassword, cancellationToken);
    }

    private async Task<Result<AccountActionAcknowledgement>> RequestAsync(string address, AccountActionPurpose purpose, CancellationToken cancellationToken)
    {
        var email = Email.Create(address);
        if (email.IsFailure) return ResultFactory.Failure<AccountActionAcknowledgement>(email.Error!);
        await actions.LockEmailAsync(email.Value!.Value, cancellationToken).ConfigureAwait(false);
        var user = await identities.FindUserByEmailAcrossTenantsAsync(email.Value.Value, cancellationToken).ConfigureAwait(false);
        var requiredStatus = purpose == AccountActionPurpose.VerifyEmail ? UserStatus.PendingVerification : UserStatus.Active;
        if (user is null || user.Status != requiredStatus || string.IsNullOrWhiteSpace(user.PasswordHash))
            return ResultFactory.Success(AccountActionAcknowledgement.Pending);
        var tenant = await identities.GetTenantForAuthenticationAsync(user.TenantId, cancellationToken).ConfigureAwait(false);
        var membership = await identities.GetTenantUserForAuthenticationAsync(user.TenantId, user.Id, cancellationToken).ConfigureAwait(false);
        if (tenant is null || tenant.Status != TenantStatus.Active || membership is null)
            return ResultFactory.Success(AccountActionAcknowledgement.Pending);
        var issued = await issuer.IssueAsync(user, purpose, cancellationToken).ConfigureAwait(false);
        return issued.IsFailure ? ResultFactory.Failure<AccountActionAcknowledgement>(issued.Error!) : ResultFactory.Success(AccountActionAcknowledgement.Pending);
    }
}
