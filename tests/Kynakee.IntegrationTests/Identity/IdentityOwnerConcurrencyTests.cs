using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Commands.ChangeUserRole;
using Kynakee.Modules.Identity.Application.Commands.DeactivateTenantUser;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class IdentityOwnerConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public async ValueTask InitializeAsync() =>
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Theory]
#pragma warning disable CA2007 // xUnit requiere conservar el contexto de ejecución de la prueba.
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OwnerMutationsDifferentMembersShouldPreserveLastActiveOwner(bool firstDeactivates, bool secondDeactivates)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TestHostEnvironment.Create());
        services.AddScoped<OwnerTenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<OwnerTenantContext>());
        services.AddIdentityModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString()
        }).Build());
        await using var provider = services.BuildServiceProvider();
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<OwnerTenantContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<OwnerTenantContext>();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IIdentityRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IIdentityRepository>();
        var tenant = Tenant.Create("Owners", TenantSlug.Create($"owners-{Guid.NewGuid():N}").Value,
            TenantType.Company, null, null, "starter").Value!;
        var firstUser = User.Create(tenant.Id, "First", "Owner", Email.Create($"{Guid.NewGuid():N}@example.test").Value,
            null, "hashed-password", UserStatus.Active).Value!;
        var secondUser = User.Create(tenant.Id, "Second", "Owner", Email.Create($"{Guid.NewGuid():N}@example.test").Value,
            null, "hashed-password", UserStatus.Active).Value!;
        var firstMember = TenantUser.Create(firstUser, UserRole.Owner).Value!;
        var secondMember = TenantUser.Create(secondUser, UserRole.Owner).Value!;
        firstContext.TenantId = tenant.Id;
        firstContext.UserId = firstUser.Id;
        secondContext.TenantId = tenant.Id;
        secondContext.UserId = secondUser.Id;
        await firstDb.Database.EnsureCreatedAsync(ct);
        firstRepository.AddTenant(tenant);
        firstRepository.AddUser(firstUser);
        firstRepository.AddUser(secondUser);
        firstRepository.AddTenantUser(firstMember);
        firstRepository.AddTenantUser(secondMember);
        await firstDb.SaveChangesAsync(ct);
        var firstVersion = firstMember.Version;
        var secondVersion = secondMember.Version;
        firstDb.ChangeTracker.Clear();
        await using var firstTransaction = await new IdentityTransactionParticipant(firstDb).BeginTransactionAsync(ct);
        await using var secondTransaction = await new IdentityTransactionParticipant(secondDb).BeginTransactionAsync(ct);

        var firstResult = await MutateAsync(firstDeactivates, firstRepository, firstContext, firstVersion, ct);
        var secondLock = secondRepository.AcquireTenantOwnerLockAsync(tenant.Id, ct);
        var waitingWindow = Task.Delay(TimeSpan.FromMilliseconds(150), ct);
        var completedBeforeCommit = await Task.WhenAny(secondLock, waitingWindow);
        Assert.Same(waitingWindow, completedBeforeCommit);
        await firstTransaction.CommitAsync(ct);
        await secondLock;
        var secondResult = await MutateAsync(secondDeactivates, secondRepository, secondContext, secondVersion, ct);
        await secondTransaction.RollbackAsync(ct);
        var remainingOwners = await secondRepository.CountActiveOwnersAsync(tenant.Id, ct);

        Assert.Equal((true, "IDENTITY_LAST_OWNER_REQUIRED", 1),
            (firstResult.IsSuccess, secondResult.Error?.Code, remainingOwners));
    }
#pragma warning restore CA2007

    [Fact]
    public async Task OwnerLocksDifferentTenantsShouldNotBlockEachOther()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TestHostEnvironment.Create());
        services.AddScoped<OwnerTenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<OwnerTenantContext>());
        services.AddIdentityModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString()
        }).Build());
        await using var provider = services.BuildServiceProvider();
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<OwnerTenantContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<OwnerTenantContext>();
        firstContext.TenantId = Guid.NewGuid();
        secondContext.TenantId = Guid.NewGuid();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IIdentityRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IIdentityRepository>();

#pragma warning disable CA2007 // xUnit requiere conservar el contexto al disponer transacciones.
        await using var firstTransaction = await new IdentityTransactionParticipant(firstDb).BeginTransactionAsync(ct);
        await using var secondTransaction = await new IdentityTransactionParticipant(secondDb).BeginTransactionAsync(ct);
        await firstRepository.AcquireTenantOwnerLockAsync(firstContext.TenantId, ct);
        var secondLock = secondRepository.AcquireTenantOwnerLockAsync(secondContext.TenantId, ct);
        await secondLock.WaitAsync(ct);

        Assert.True(secondLock.IsCompletedSuccessfully);
#pragma warning restore CA2007
    }

    [Fact]
    public async Task OwnerLockWithoutTransactionShouldRejectExecution()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var context = new OwnerTenantContext { TenantId = Guid.NewGuid(), UserId = Guid.NewGuid() };
        services.AddSingleton<ITenantContext>(context);
        services.AddIdentityModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString()
        }).Build());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIdentityRepository>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.AcquireTenantOwnerLockAsync(context.TenantId, TestContext.Current.CancellationToken));
    }

    private static Task<Result> MutateAsync(bool deactivates, IIdentityRepository repository,
        OwnerTenantContext context, uint version, CancellationToken ct) => deactivates
        ? new DeactivateTenantUserCommandHandler(repository, context, TimeProvider.System)
            .Handle(new DeactivateTenantUserCommand(context.TenantId, context.UserId, version), ct)
        : new ChangeUserRoleCommandHandler(repository, context)
            .Handle(new ChangeUserRoleCommand(context.TenantId, context.UserId, UserRole.Admin, version), ct);

    private sealed class OwnerTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public bool IsAuthenticated => true;
    }
}
