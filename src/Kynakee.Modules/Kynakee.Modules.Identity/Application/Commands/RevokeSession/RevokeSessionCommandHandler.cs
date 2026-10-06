using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.RevokeSession;

public sealed class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, Result>
{
    private readonly IIdentityRepository _repository;
    private readonly IIdentityTokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public RevokeSessionCommandHandler(
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

    public async Task<Result> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = await _repository.FindRefreshTokenByHashAsync(
                _tokenService.HashToken(request.RefreshToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (token is null)
        {
            return ResultFactory.Ok();
        }

        return token.Revoke(_timeProvider.GetUtcNow());
    }
}