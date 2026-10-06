using FluentAssertions;
using Kynakee.Modules.Billing.Domain.Aggregates;
using Kynakee.Modules.Billing.Domain.Entities;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.Billing.Infrastructure.Persistence;
using Kynakee.Modules.Billing.Infrastructure.Repositories;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Billing;

public sealed class BillingPostgresPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly TestTenantContext _tenantContext = new();
    private readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact]
    public async Task MigrationFreshDatabaseShouldApplyInitialBillingSchema()
    {
        await using var context = CreateContext();

        var migrations = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        migrations.Should().ContainSingle().Which.Should().EndWith("_InitialBillingSchema");
    }

    [Fact]
    public async Task RepositoryReloadShouldPreserveReservationAllocations()
    {
        var operationId = Guid.NewGuid();
        await SeedAsync(operationId);
        await using var context = CreateContext();
        var repository = new EfBillingRepository(context);

        var account = await repository.GetAccountForUpdateAsync(_tenantContext.TenantId, TestContext.Current.CancellationToken);

        account!.Transactions.Single(transaction => transaction.OperationId == operationId)
            .Allocations.Should().ContainSingle().Which.Amount.Should().Be(30m);
    }

    [Fact]
    public async Task RepositoryReloadedReservationShouldPersistConsumption()
    {
        var operationId = Guid.NewGuid();
        await SeedAsync(operationId);
        await using (var context = CreateContext())
        {
            var repository = new EfBillingRepository(context);
            var account = await repository.GetAccountForUpdateAsync(_tenantContext.TenantId, TestContext.Current.CancellationToken);
            account!.Consume(operationId, _now.AddMinutes(1)).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verification = CreateContext();
        var balance = await new EfBillingRepository(verification).GetBalanceAsync(
            _tenantContext.TenantId, _now, TestContext.Current.CancellationToken);

        balance!.AvailableCredits.Should().Be(70m);
    }

    [Fact]
    public async Task RepositoryBalanceShouldRetainExpiredLotReservations()
    {
        await SeedAsync(Guid.NewGuid());
        await using var context = CreateContext();

        var balance = await new EfBillingRepository(context).GetBalanceAsync(
            _tenantContext.TenantId, _now.AddMonths(4), TestContext.Current.CancellationToken);

        balance.Should().BeEquivalentTo(new
        {
            AvailableCredits = 0m,
            ReservedCredits = 30m,
            PlanId = "starter",
            NextRenewalDate = (DateTime?)_now.AddMonths(1)
        });
    }

    [Fact]
    public async Task RepositoryHistoryCursorShouldReturnRemainingTransactions()
    {
        await SeedAsync(Guid.NewGuid());
        await using var context = CreateContext();
        var repository = new EfBillingRepository(context);
        var firstPage = await repository.GetTransactionsAsync(
            _tenantContext.TenantId, null, null, null, null, 1, TestContext.Current.CancellationToken);

        var secondPage = await repository.GetTransactionsAsync(
            _tenantContext.TenantId, null, null, null, firstPage.NextCursor, 1, TestContext.Current.CancellationToken);

        secondPage.Items.Should().ContainSingle().Which.Id.Should().NotBe(firstPage.Items.Single().Id);
    }

    [Fact]
    public async Task RepositoryDifferentTenantShouldNotExposeAccount()
    {
        await SeedAsync(null);
        var originalTenant = _tenantContext.TenantId;
        _tenantContext.TenantId = Guid.NewGuid();
        await using var context = CreateContext();

        var account = await new EfBillingRepository(context).GetAccountForUpdateAsync(
            originalTenant, TestContext.Current.CancellationToken);

        account.Should().BeNull();
    }

    [Fact]
    public async Task RepositoryDeletedAccountShouldNotExposeBalance()
    {
        await SeedAsync(null);
        await using (var context = CreateContext())
        {
            var account = await context.CreditAccounts.SingleAsync(TestContext.Current.CancellationToken);
            account.Delete();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verification = CreateContext();
        var balance = await new EfBillingRepository(verification).GetBalanceAsync(
            _tenantContext.TenantId, _now, TestContext.Current.CancellationToken);

        balance.Should().BeNull();
    }

    [Fact]
    public async Task AccountConcurrentReservationsShouldRejectStaleWrite()
    {
        await SeedAsync(null);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstAccount = await new EfBillingRepository(firstContext).GetAccountForUpdateAsync(
            _tenantContext.TenantId, TestContext.Current.CancellationToken);
        var secondAccount = await new EfBillingRepository(secondContext).GetAccountForUpdateAsync(
            _tenantContext.TenantId, TestContext.Current.CancellationToken);
        firstAccount!.Reserve(60m, Guid.NewGuid(), _now.AddMinutes(15), _now);
        secondAccount!.Reserve(60m, Guid.NewGuid(), _now.AddMinutes(15), _now);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Func<Task> saveStaleAccount = () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await saveStaleAccount.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private BillingDbContext CreateContext() => new(
        new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
        _tenantContext);

    private async Task SeedAsync(Guid? operationId)
    {
        var planId = PlanId.Create("starter").Value;
        var account = CreditAccount.Create(_tenantContext.TenantId, planId).Value!;
        account.Recharge(100m, CreditSource.Subscription, _now.AddMonths(3), _now);
        if (operationId.HasValue)
        {
            account.Reserve(30m, operationId.Value, _now.AddMinutes(15), _now);
        }

        var subscription = Subscription.Create(
            _tenantContext.TenantId, planId, BillingCycle.Monthly, _now, _now.AddMonths(1), 100).Value!;
        using var context = CreateContext();
        var repository = new EfBillingRepository(context);
        repository.AddAccount(account);
        repository.AddSubscription(subscription);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
    }
}
