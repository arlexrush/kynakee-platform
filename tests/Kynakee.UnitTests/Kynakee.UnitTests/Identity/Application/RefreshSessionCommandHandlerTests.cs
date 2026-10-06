using FluentAssertions;
using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Commands.RefreshSession;
using Kynakee.Modules.Identity.Application.Contracts;
using NSubstitute;
using Xunit;

namespace Kynakee.UnitTests.Identity.Application;

public class RefreshSessionCommandHandlerTests
{
    [Fact]
    public async Task HandleShouldRejectUnknownRefreshToken()
    {
        var repository = Substitute.For<IIdentityRepository>();
        var tokenService = Substitute.For<IIdentityTokenService>();
        tokenService.HashToken("opaque-token").Returns("hashed-token");
        repository.FindRefreshTokenByHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns((Kynakee.Modules.Identity.Domain.Entities.RefreshToken?)null);
        var handler = new RefreshSessionCommandHandler(
            repository,
            tokenService,
            TimeProvider.System);

        var result = await handler.Handle(new RefreshSessionCommand("opaque-token"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("IDENTITY_REFRESH_TOKEN_INVALID");
    }
}