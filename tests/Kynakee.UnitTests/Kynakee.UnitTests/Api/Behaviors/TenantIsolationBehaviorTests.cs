using System.Security.Claims;
using FluentAssertions;
using Kynakee.Api.Application.Abstractions;
using Kynakee.Api.Application.Behaviors;
using Kynakee.Modules.SharedKernel.Application;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Kynakee.UnitTests.Api.Behaviors;

public sealed class TenantIsolationBehaviorTests
{
    [Fact]
    public async Task HandleWithAuthenticatedTenantAndSubjectClaimsShouldInitializeTenantContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = CreateContext(
            new ClaimsIdentity(
                [new Claim("tenant_id", tenantId.ToString()), new Claim("sub", userId.ToString())],
                "UnitTest"));
        var tenantContext = new HttpTenantContext();
        var behavior = new TenantIsolationBehavior<TestRequest, Result<TestResponse>>(
            context,
            tenantContext);
        var handlerInvoked = false;

        var result = await behavior.Handle(
            new TestRequest(),
            _ =>
            {
                handlerInvoked = true;
                return Task.FromResult(ResultFactory.Success(new TestResponse()));
            },
            CancellationToken.None);

        tenantContext.TenantId.Should().Be(tenantId);
        tenantContext.UserId.Should().Be(userId);
        tenantContext.IsAuthenticated.Should().BeTrue();
        handlerInvoked.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleWithAuthenticatedIdentityMissingTenantClaimShouldReturnUnauthorized()
    {
        var context = CreateContext(
            new ClaimsIdentity(
                [new Claim("sub", Guid.NewGuid().ToString())],
                "UnitTest"));
        var tenantContext = new HttpTenantContext();
        var behavior = new TenantIsolationBehavior<TestRequest, Result<TestResponse>>(
            context,
            tenantContext);
        var handlerInvoked = false;

        var result = await behavior.Handle(
            new TestRequest(),
            _ =>
            {
                handlerInvoked = true;
                return Task.FromResult(ResultFactory.Success(new TestResponse()));
            },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeEquivalentTo(new
        {
            Code = "TENANT_001",
            Type = ErrorType.Unauthorized
        });
        handlerInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task HandleWithoutAuthenticatedIdentityShouldInitializeAnonymousContextAndInvokeHandler()
    {
        var context = CreateContext(new ClaimsIdentity());
        var tenantContext = new HttpTenantContext();
        var behavior = new TenantIsolationBehavior<TestRequest, Result<TestResponse>>(
            context,
            tenantContext);
        var handlerInvoked = false;

        var result = await behavior.Handle(
            new TestRequest(),
            _ =>
            {
                handlerInvoked = true;
                return Task.FromResult(ResultFactory.Success(new TestResponse()));
            },
            CancellationToken.None);

        tenantContext.TenantId.Should().Be(Guid.Empty);
        tenantContext.UserId.Should().Be(Guid.Empty);
        tenantContext.IsAuthenticated.Should().BeFalse();
        handlerInvoked.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    private static HttpContextAccessor CreateContext(ClaimsIdentity identity) =>
        new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

    private sealed record TestRequest;

    private sealed record TestResponse;
}
