using FluentAssertions;
using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Commands.AcceptInvitation;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Kynakee.UnitTests.Identity.Application;

public sealed class AcceptInvitationCommandHandlerTests
{
    [Fact]
    public async Task AcceptInvitationAnonymousCallerShouldActivateInvitedUser()
    {
        var repository = Substitute.For<IIdentityRepository>();
        var tokens = Substitute.For<IIdentityTokenService>();
        var tenant = Tenant.Create("Tenant", TenantSlug.Create("tenant").Value, TenantType.Company, null, null, "starter").Value!;
        var user = User.Create(tenant.Id, "Invited", "User", Email.Create("invited@example.test").Value,
            null, null, UserStatus.PendingVerification).Value!;
        var membership = TenantUser.Create(user, UserRole.Admin).Value!;
        var hash = new string('A', 64);
        var invitation = UserInvitation.Create(tenant.Id, user.Id, hash, DateTimeOffset.UtcNow).Value!;
        tokens.HashToken(Arg.Any<string>()).Returns(hash);
        tokens.GenerateOpaqueToken().Returns("refresh-token");
        tokens.CreateAccessToken(user, tenant, membership).Returns("access-token");
        repository.FindInvitationByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(invitation);
        repository.GetUserForAuthenticationAsync(tenant.Id, user.Id, Arg.Any<CancellationToken>()).Returns(user);
        repository.GetTenantForAuthenticationAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        repository.GetTenantUserForAuthenticationAsync(tenant.Id, user.Id, Arg.Any<CancellationToken>()).Returns(membership);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<ITenantContext>());
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Testing");
        services.AddSingleton(environment);
        services.AddIdentityModule(new ConfigurationBuilder().Build());
        services.AddScoped(_ => repository);
        services.AddSingleton(tokens);
        services.AddTransient<AcceptInvitationCommandHandler>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AcceptInvitationCommandHandler>();

        var result = await handler.Handle(new AcceptInvitationCommand("invitation-token", "ValidPassword#123"), CancellationToken.None);

        result.Value!.User.Status.Should().Be(UserStatus.Active);
    }
}
