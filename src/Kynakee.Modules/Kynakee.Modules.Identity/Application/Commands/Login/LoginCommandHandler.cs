using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Microsoft.AspNetCore.Identity;

using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<IdentitySessionResponse>>
{
    private readonly IIdentityRepository _repository;
    private readonly UserManager<Domain.Aggregates.User> _userManager;
    private readonly IIdentityTokenService _tokenService;
    private readonly TimeProvider _timeProvider;
    private readonly IAccountActionRepository _actions;

    public LoginCommandHandler(
        IIdentityRepository repository,
        UserManager<Domain.Aggregates.User> userManager,
        IIdentityTokenService tokenService,
        TimeProvider timeProvider,
        IAccountActionRepository actions)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(actions);
        _repository = repository;
        _userManager = userManager;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
        _actions = actions;
    }

    public async Task<Result<IdentitySessionResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = Email.Create(request.Email);
        if (email.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(email.Error!);
        }

        await _actions.LockEmailAsync(email.Value!.Value, cancellationToken).ConfigureAwait(false);
        var user = await _repository.FindUserByEmailAcrossTenantsAsync(
                email.Value!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (user is null || user.Status != UserStatus.Active)
        {
            return InvalidCredentials();
        }

        var passwordResult = await _userManager.CheckPasswordAsync(user, request.Password)
            .ConfigureAwait(false);
        if (!passwordResult)
        {
            return InvalidCredentials();
        }

        var tenant = await _repository.GetTenantForAuthenticationAsync(user.TenantId, cancellationToken)
            .ConfigureAwait(false);
        var membership = await _repository.GetTenantUserForAuthenticationAsync(
                user.TenantId,
                user.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null || tenant.Status != TenantStatus.Active || membership is null)
        {
            return InvalidCredentials();
        }

        return IdentitySessionFactory.Create(
            user,
            tenant,
            membership,
            _repository,
            _tokenService,
            _timeProvider.GetUtcNow());
    }

    private static Result<IdentitySessionResponse> InvalidCredentials() =>
        ResultFactory.Failure<IdentitySessionResponse>(
            ApplicationError.Unauthorized("IDENTITY_CREDENTIALS_INVALID", "Email or password is invalid."));
}