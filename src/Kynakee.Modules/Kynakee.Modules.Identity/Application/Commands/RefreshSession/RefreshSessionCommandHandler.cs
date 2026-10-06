using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.RefreshSession;

public sealed class RefreshSessionCommandHandler
    : IRequestHandler<RefreshSessionCommand, Result<IdentitySessionResponse>>
{
    private readonly IIdentityRepository _repository;
    private readonly IIdentityTokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public RefreshSessionCommandHandler(
        IIdentityRepository repository,
        IIdentityTokenService tokenService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IdentitySessionResponse>> Handle(
        RefreshSessionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _timeProvider.GetUtcNow();
        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var currentToken = await _repository.FindRefreshTokenByHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);
        if (currentToken is null || !currentToken.IsUsableAt(now))
        {
            return InvalidRefreshToken();
        }

        var user = await _repository.GetUserForAuthenticationAsync(
                currentToken.TenantId,
                currentToken.UserId,
                cancellationToken)
            .ConfigureAwait(false);
        var tenant = await _repository.GetTenantForAuthenticationAsync(currentToken.TenantId, cancellationToken)
            .ConfigureAwait(false);
        var membership = await _repository.GetTenantUserForAuthenticationAsync(
                currentToken.TenantId,
                currentToken.UserId,
                cancellationToken)
            .ConfigureAwait(false);

        if (user is null || user.Status != UserStatus.Active ||
            tenant is null || tenant.Status != TenantStatus.Active || membership is null)
        {
            return InvalidRefreshToken();
        }

        var newRawToken = _tokenService.GenerateOpaqueToken();
        var newTokenResult = Domain.Entities.RefreshToken.Create(
            currentToken.TenantId,
            currentToken.UserId,
            _tokenService.HashToken(newRawToken),
            now);
        if (newTokenResult.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(newTokenResult.Error!);
        }

        var newToken = newTokenResult.Value!;
        var revokeResult = currentToken.Revoke(now, newToken.Id);
        if (revokeResult.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(revokeResult.Error!);
        }

        _repository.AddRefreshToken(newToken);
        var accessToken = _tokenService.CreateAccessToken(user, tenant, membership);
        return ResultFactory.Success(new IdentitySessionResponse(
            accessToken,
            newRawToken,
            IdentitySessionFactory.AccessTokenLifetimeSeconds,
            "Bearer",
            IdentitySessionFactory.MapUser(user, membership),
            IdentitySessionFactory.MapTenant(tenant)));
    }

    private static Result<IdentitySessionResponse> InvalidRefreshToken() =>
        ResultFactory.Failure<IdentitySessionResponse>(
            ApplicationError.Unauthorized("IDENTITY_REFRESH_TOKEN_INVALID", "Refresh token is invalid or expired."));
}