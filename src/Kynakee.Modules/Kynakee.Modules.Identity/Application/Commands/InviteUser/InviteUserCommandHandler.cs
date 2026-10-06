using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.InviteUser;

public sealed class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, Result<InvitationCreatedResponse>>
{
    private readonly IIdentityRepository _repository;
    private readonly IIdentityTokenService _tokenService;
    private readonly IInvitationEmailSender _emailSender;
    private readonly IPostCommitActionDispatcher _postCommitDispatcher;
    private readonly TimeProvider _timeProvider;

    public InviteUserCommandHandler(
        IIdentityRepository repository,
        IIdentityTokenService tokenService,
        IInvitationEmailSender emailSender,
        IPostCommitActionDispatcher postCommitDispatcher,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(postCommitDispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _postCommitDispatcher = postCommitDispatcher;
        _timeProvider = timeProvider;
    }

    public async Task<Result<InvitationCreatedResponse>> Handle(
        InviteUserCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inviterMembership = await _repository.GetTenantUserAsync(
                request.TenantId,
                request.InviterUserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (inviterMembership is null || inviterMembership.Role is not (UserRole.Owner or UserRole.Admin))
        {
            return ResultFactory.Failure<InvitationCreatedResponse>(
                ApplicationError.Unauthorized("IDENTITY_INVITATION_FORBIDDEN", "Only tenant owners and administrators can invite users."));
        }

        var inviter = await _repository.GetUserAsync(
                request.TenantId,
                request.InviterUserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (inviter is null || inviter.Status != UserStatus.Active)
        {
            return ResultFactory.Failure<InvitationCreatedResponse>(ApplicationError.Unauthorized(
                "IDENTITY_INVITATION_FORBIDDEN", "An active tenant user is required to invite users."));
        }

        var inviterTenant = await _repository.GetTenantAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (inviterTenant is null || inviterTenant.Status != TenantStatus.Active)
        {
            return ResultFactory.Failure<InvitationCreatedResponse>(ApplicationError.NotFound(
                "IDENTITY_TENANT_NOT_FOUND", "The active tenant was not found."));
        }

        var email = Email.Create(request.Email);
        var phone = request.Phone is null ? null : PhoneNumber.Create(request.Phone);
        if (email.IsFailure || phone?.IsFailure == true)
        {
            return ResultFactory.Failure<InvitationCreatedResponse>(email.Error ?? phone!.Error!);
        }

        if (await _repository.FindUserByEmailAcrossTenantsAsync(email.Value!.Value, cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            return ResultFactory.Failure<InvitationCreatedResponse>(
                ApplicationError.Conflict("IDENTITY_EMAIL_ALREADY_REGISTERED", "An account with this email already exists."));
        }

        var userResult = User.Create(
            request.TenantId,
            request.FirstName,
            request.LastName,
            email.Value,
            phone?.Value,
            null,
            UserStatus.PendingVerification,
            request.InviterUserId);
        if (userResult.IsFailure)
        {
            return ResultFactory.Failure<InvitationCreatedResponse>(userResult.Error!);
        }

        var user = userResult.Value!;
        var membershipResult = TenantUser.Create(user, request.Role, request.InviterUserId);
        if (membershipResult.IsFailure)
        {
            return ResultFactory.Failure<InvitationCreatedResponse>(membershipResult.Error!);
        }

        var rawToken = _tokenService.GenerateOpaqueToken();
        var now = _timeProvider.GetUtcNow();
        var invitationResult = UserInvitation.Create(
            request.TenantId,
            user.Id,
            _tokenService.HashToken(rawToken),
            now,
            request.InviterUserId);
        if (invitationResult.IsFailure)
        {
            return ResultFactory.Failure<InvitationCreatedResponse>(invitationResult.Error!);
        }

        _repository.AddUser(user);
        _repository.AddTenantUser(membershipResult.Value!);
        _repository.AddInvitation(invitationResult.Value!);
        _postCommitDispatcher.Enqueue(token => _emailSender.SendInvitationAsync(
            user.Email.Value,
            inviterTenant.Name,
            rawToken,
            token));

        return ResultFactory.Success(new InvitationCreatedResponse(
            invitationResult.Value!.Id,
            invitationResult.Value.ExpiresAt));
    }
}