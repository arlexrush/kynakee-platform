using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Commands;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Kynakee.Modules.Identity.Application.Commands.AcceptInvitation;

public sealed class AcceptInvitationCommandHandler
    : IRequestHandler<AcceptInvitationCommand, Result<IdentitySessionResponse>>
{
    private readonly IIdentityRepository _repository;
    private readonly UserManager<Domain.Aggregates.User> _userManager;
    private readonly IIdentityTokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public AcceptInvitationCommandHandler(
        IIdentityRepository repository,
        UserManager<Domain.Aggregates.User> userManager,
        IIdentityTokenService tokenService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _userManager = userManager;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IdentitySessionResponse>> Handle(
        AcceptInvitationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _timeProvider.GetUtcNow();
        var invitation = await _repository.FindInvitationByHashAsync(
                _tokenService.HashToken(request.InvitationToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (invitation is null || !invitation.IsUsableAt(now))
        {
            return ResultFactory.Failure<IdentitySessionResponse>(
                ApplicationError.Unauthorized("IDENTITY_INVITATION_UNAVAILABLE", "Invitation is invalid or expired."));
        }

        var user = await _repository.GetUserForAuthenticationAsync(invitation.TenantId, invitation.UserId, cancellationToken)
            .ConfigureAwait(false);
        var tenant = await _repository.GetTenantForAuthenticationAsync(invitation.TenantId, cancellationToken)
            .ConfigureAwait(false);
        var membership = await _repository.GetTenantUserAsync(invitation.TenantId, invitation.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null || tenant is null || tenant.Status != TenantStatus.Active ||
            membership is null || user.Status != UserStatus.PendingVerification)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(
                ApplicationError.Conflict("IDENTITY_INVITATION_UNAVAILABLE", "Invitation is no longer available."));
        }

        var passwordResult = await _userManager.AddPasswordAsync(user, request.Password)
            .ConfigureAwait(false);
        if (!passwordResult.Succeeded)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(
                ApplicationError.Validation("IDENTITY_PASSWORD_REJECTED", "The password does not satisfy the identity policy."));
        }

        var acceptResult = user.AcceptInvitation(user.PasswordHash);
        if (acceptResult.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(acceptResult.Error!);
        }

        var invitationAcceptResult = invitation.Accept(now);
        if (invitationAcceptResult.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(invitationAcceptResult.Error!);
        }

        return IdentitySessionFactory.Create(
            user,
            tenant,
            membership,
            _repository,
            _tokenService,
            now);
    }
}