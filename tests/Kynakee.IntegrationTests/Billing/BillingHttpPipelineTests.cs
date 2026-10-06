using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Kynakee.Api.Application.Abstractions;
using Kynakee.Api.Application.Behaviors;
using Kynakee.Api.Endpoints;
using Kynakee.Api.Infrastructure.Persistence;
using Kynakee.Modules.Billing;
using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.Billing.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kynakee.IntegrationTests.Billing;

public sealed class BillingHttpPipelineTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly Guid _tenantId = Guid.NewGuid();
    private WebApplication _app = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString()
        });
        builder.Services.AddBillingModule(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<HttpTenantContext>();
        builder.Services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<HttpTenantContext>());
        builder.Services.AddScoped<ITenantContextWriter>(provider => provider.GetRequiredService<HttpTenantContext>());
        builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();
        builder.Services.AddScoped<ITransactionManager, ModuleTransactionManager>();
        builder.Services.AddScoped<IDomainEventCollector, ModuleDomainEventCollector>();
        builder.Services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        builder.Services.AddScoped<IPostCommitActionDispatcher, PostCommitActionDispatcher>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantIsolationBehavior<,>));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TokenGateBehavior<,>));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainEventDispatchBehavior<,>));
        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            var suppliedTenant = context.Request.Headers["X-Test-Tenant"].ToString();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant_id", suppliedTenant), new Claim("sub", Guid.NewGuid().ToString())], "Test"));
            await next(context).ConfigureAwait(false);
        });
        _app.UseAuthorization();
        _app.MapBillingEndpoints();
        await _app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        using var scope = _app.Services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<HttpTenantContext>();
        tenant.Initialize(_tenantId, Guid.NewGuid(), true);
        var database = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var account = CreditAccount.Create(_tenantId, PlanId.Create("starter").Value).Value!;
        account.Recharge(100m, CreditSource.Manual, now.AddMonths(3), now);
        database.CreditAccounts.Add(account);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task BalanceAuthenticatedTenantShouldReturnCredits()
    {
        using var client = CreateClient();

        var response = await client.GetFromJsonAsync<ApiResponse<CreditBalanceDto>>(
            "/api/v1/billing/credits", TestContext.Current.CancellationToken);

        response!.Data.AvailableCredits.Should().Be(100m);
    }

    [Fact]
    public async Task BalanceMissingTenantClaimShouldReturnUnauthorized()
    {
        using var client = _app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/api/v1/billing/credits", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HistoryInvalidLimitShouldReturnBadRequest()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/billing/credits/transactions?limit=0", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HistoryRechargeFilterShouldReturnRecharge()
    {
        using var client = CreateClient();

        var response = await client.GetFromJsonAsync<ApiResponse<CreditTransactionHistoryDto>>(
            "/api/v1/billing/credits/transactions?type=Recharge&from=2020-01-01&limit=1", TestContext.Current.CancellationToken);

        response!.Data.Items.Should().ContainSingle().Which.Type.Should().Be(CreditTransactionKind.Recharge);
    }

    [Theory]
    [InlineData(true, 90)]
    [InlineData(false, 100)]
    public async Task TokenGateHandlerOutcomeShouldFinalizeReservation(bool succeeds, int expectedCredits)
    {
        using var scope = _app.Services.CreateScope();
        InitializeClaims(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<ITokenGateService>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var gate = new TokenGateBehavior<TestCreditRequest, Result>(tenant, service);

        await gate.Handle(new TestCreditRequest(10), _ => Task.FromResult(succeeds
            ? ResultFactory.Ok()
            : ResultFactory.Failure(ApplicationError.Conflict("TEST_FAILED", "Test failure."))), TestContext.Current.CancellationToken);
        var balance = await scope.ServiceProvider.GetRequiredService<IBillingService>()
            .GetBalanceAsync(_tenantId, TestContext.Current.CancellationToken);

        balance.Value!.AvailableCredits.Should().Be(expectedCredits);
    }

    [Fact]
    public async Task TokenGateInsufficientCreditsShouldNotInvokeHandler()
    {
        using var scope = _app.Services.CreateScope();
        InitializeClaims(scope.ServiceProvider);
        var gate = new TokenGateBehavior<TestCreditRequest, Result>(
            scope.ServiceProvider.GetRequiredService<ITenantContext>(), scope.ServiceProvider.GetRequiredService<ITokenGateService>());
        var invoked = false;

        await gate.Handle(new TestCreditRequest(101), _ =>
        {
            invoked = true;
            return Task.FromResult(ResultFactory.Ok());
        }, TestContext.Current.CancellationToken);

        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task TokenGateHandlerExceptionShouldReleaseReservation()
    {
        using var scope = _app.Services.CreateScope();
        InitializeClaims(scope.ServiceProvider);
        var gate = new TokenGateBehavior<TestCreditRequest, Result>(
            scope.ServiceProvider.GetRequiredService<ITenantContext>(), scope.ServiceProvider.GetRequiredService<ITokenGateService>());
        Func<Task> execute = () => gate.Handle(
            new TestCreditRequest(10),
            _ => throw new InvalidOperationException("Test handler failure."),
            TestContext.Current.CancellationToken);

        await execute.Should().ThrowAsync<InvalidOperationException>();
        var balance = await scope.ServiceProvider.GetRequiredService<IBillingService>()
            .GetBalanceAsync(_tenantId, TestContext.Current.CancellationToken);

        balance.Value!.AvailableCredits.Should().Be(100m);
    }

    [Fact]
    public async Task InitializationNewTenantShouldPersistInitialBalanceAndSubscription()
    {
        using var scope = _app.Services.CreateScope();
        var tenantId = Guid.NewGuid();
        InitializeClaims(scope.ServiceProvider, tenantId);
        var billing = scope.ServiceProvider.GetRequiredService<IBillingService>();

        var result = await billing.InitializeCreditAccountAsync(tenantId, "starter", TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        var balance = await billing.GetBalanceAsync(tenantId, TestContext.Current.CancellationToken);

        balance.Value.Should().Match<CreditBalanceDto>(value =>
            value.AvailableCredits == 500m && value.ReservedCredits == 0m &&
            value.PlanId == "starter" && value.NextRenewalDate.HasValue);
    }

    [Fact]
    public async Task InitializationRepeatedPlanShouldNotDuplicateCreditsOrSubscription()
    {
        using var scope = _app.Services.CreateScope();
        var tenantId = Guid.NewGuid();
        InitializeClaims(scope.ServiceProvider, tenantId);
        var billing = scope.ServiceProvider.GetRequiredService<IBillingService>();
        await billing.InitializeCreditAccountAsync(tenantId, "starter", TestContext.Current.CancellationToken);

        var result = await billing.InitializeCreditAccountAsync(tenantId, "STARTER", TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        var database = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        database.ChangeTracker.Clear();
        var persisted = new
        {
            Accounts = await database.CreditAccounts.CountAsync(TestContext.Current.CancellationToken),
            Subscriptions = await database.Subscriptions.CountAsync(TestContext.Current.CancellationToken),
            Lots = await database.CreditLots.CountAsync(TestContext.Current.CancellationToken),
            Transactions = await database.CreditTransactions.CountAsync(TestContext.Current.CancellationToken),
            Credits = await database.CreditLots.SumAsync(lot => lot.AvailableCredits, TestContext.Current.CancellationToken)
        };

        persisted.Should().BeEquivalentTo(new { Accounts = 1, Subscriptions = 1, Lots = 1, Transactions = 1, Credits = 500m });
    }

    [Fact]
    public async Task InitializationDifferentPlanShouldReturnConflict()
    {
        using var scope = _app.Services.CreateScope();
        var tenantId = Guid.NewGuid();
        InitializeClaims(scope.ServiceProvider, tenantId);
        var billing = scope.ServiceProvider.GetRequiredService<IBillingService>();
        await billing.InitializeCreditAccountAsync(tenantId, "starter", TestContext.Current.CancellationToken);

        var result = await billing.InitializeCreditAccountAsync(tenantId, "pro", TestContext.Current.CancellationToken);

        result.Error.Should().Match<ApplicationError>(error =>
            error.Code == "BILL_ACCOUNT_ALREADY_INITIALIZED" && error.Type == ErrorType.Conflict);
    }

    private HttpClient CreateClient()
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _tenantId.ToString());
        return client;
    }

    private void InitializeClaims(IServiceProvider provider, Guid? tenantId = null)
    {
        var currentTenantId = tenantId ?? _tenantId;
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant_id", currentTenantId.ToString()), new Claim("sub", Guid.NewGuid().ToString())], "Test"))
        };
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        provider.GetRequiredService<HttpTenantContext>().Initialize(currentTenantId, Guid.NewGuid(), true);
    }

    private sealed record TestCreditRequest(int CreditsRequired) : IRequiresCredits;
}
