using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Mail;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class InvitationEmailDurabilityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly DeliveryClock _clock = new();
    private readonly ControlledSender _sender = new();
    private ServiceProvider _provider = null!;
    private Guid _tenantId;
    private Guid _userId;
    private Guid _invitationId;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _postgres.StartAsync(ct).ConfigureAwait(true);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["Identity:InvitationEmail:ActiveKeyId"] = "v1",
            ["Identity:InvitationEmail:Keys:v1"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TestHostEnvironment.Create());
        services.AddSingleton<TimeProvider>(_clock);
        services.AddScoped<ITenantContext>(_ => new DeliveryTenantContext(_tenantId, _userId));
        services.AddIdentityModule(configuration);
        services.AddSingleton(new InvitationEmailPayloadProtector(configuration));
        services.AddScoped<IInvitationEmailJobStore, InvitationEmailJobStore>();
        services.AddSingleton<IInvitationEmailSender>(_sender);
        services.AddScoped<InvitationEmailJobProcessor>();
        _provider = services.BuildServiceProvider();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(true);
        var tenant = Tenant.Create("Delivery", TenantSlug.Create("delivery").Value, TenantType.Company, null, null, "starter").Value!;
        var user = User.Create(tenant.Id, "Invited", "User", Email.Create("invited@example.test").Value, null, null, UserStatus.PendingVerification).Value!;
        var invitation = UserInvitation.Create(tenant.Id, user.Id, new string('A', 64), _clock.GetUtcNow()).Value!;
        _tenantId = tenant.Id;
        _userId = user.Id;
        _invitationId = invitation.Id;
        db.AddRange(tenant, user, TenantUser.Create(user, UserRole.Viewer).Value!, invitation);
        await db.SaveChangesAsync(ct).ConfigureAwait(true);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync().ConfigureAwait(true);
        await _postgres.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task EmailJobCommittedShouldSurviveNewScopeAndClearPayloadAfterDelivery()
    {
        await EnqueueAsync(true);
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);
        using var verify = _provider.CreateScope();
        var job = await verify.ServiceProvider.GetRequiredService<IdentityDbContext>().InvitationEmailJobs.IgnoreQueryFilters()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((1, true, 0), (_sender.Calls, job.CompletedAt is not null, job.EncryptedPayload.Length));
    }

    [Fact]
    public async Task EmailJobRolledBackShouldNotRemainOrSend()
    {
        await EnqueueAsync(false);
        using var scope = _provider.CreateScope();
        var worked = await scope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);
        Assert.Equal((false, 0), (worked, _sender.Calls));
    }

    [Fact]
    public async Task EmailJobCancelledProcessingShouldRemainRecoverable()
    {
        await EnqueueAsync(true);
        _sender.Hold = true;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using (var scope = _provider.CreateScope())
        {
            var execution = scope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(cancellation.Token);
            await _sender.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        }
        _sender.Hold = false;
        using var retryScope = _provider.CreateScope();
        var worked = await retryScope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, 2), (worked, _sender.Calls));
    }

    [Fact]
    public async Task EmailJobTransportFailureShouldPersistRetryAndDeliverLater()
    {
        await EnqueueAsync(true);
        _sender.Fail = true;
        using (var scope = _provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);
        }
        _sender.Fail = false;
        _clock.Advance(TimeSpan.FromMinutes(1));
        using var retryScope = _provider.CreateScope();
        await retryScope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);
        using var verify = _provider.CreateScope();
        var job = await verify.ServiceProvider.GetRequiredService<IdentityDbContext>().InvitationEmailJobs.IgnoreQueryFilters()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((2, 1, true), (_sender.Calls, job.Attempts, job.CompletedAt is not null));
    }

    [Fact]
    public async Task EmailJobRevokedInvitationShouldNotSend()
    {
        await EnqueueAsync(true);
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            (await db.UserInvitations.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken)).Revoke(_clock.GetUtcNow());
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var processScope = _provider.CreateScope();
        await processScope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _sender.Calls);
    }

    [Fact]
    public async Task EmailJobExpiredShouldClearPayloadWithoutSending()
    {
        await EnqueueAsync(true);
        _clock.Advance(TimeSpan.FromHours(25));
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);
        using var verify = _provider.CreateScope();
        var job = await verify.ServiceProvider.GetRequiredService<IdentityDbContext>().InvitationEmailJobs.IgnoreQueryFilters()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal((0, true, 0), (_sender.Calls, job.CompletedAt is not null, job.EncryptedPayload.Length));
    }

    [Fact]
    public async Task InvitationAndEmailJobSameTransactionRollbackShouldPersistNeither()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var transaction = await new IdentityTransactionParticipant(db).BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (transaction.ConfigureAwait(true))
        {
            var invitation = UserInvitation.Create(_tenantId, _userId, new string('B', 64), _clock.GetUtcNow()).Value!;
            db.UserInvitations.Add(invitation);
            scope.ServiceProvider.GetRequiredService<IInvitationEmailJobStore>().Enqueue(_tenantId, invitation.Id, _userId,
                "invited@example.test", "Delivery", "secret-token", invitation.ExpiresAt);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
        using var verify = _provider.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var invitations = await verifyDb.UserInvitations.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        var jobs = await verifyDb.InvitationEmailJobs.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal((1, 0), (invitations, jobs));
    }

    [Fact]
    public async Task EmailJobConcurrentWorkersShouldSkipLockedJob()
    {
        await EnqueueAsync(true);
        _sender.Hold = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var firstScope = _provider.CreateScope();
        using var secondScope = _provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(timeout.Token);
        await _sender.Started.Task.WaitAsync(timeout.Token);
        var second = await secondScope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>().ProcessNextAsync(timeout.Token);
        _sender.Release.TrySetResult();
        await first.ConfigureAwait(true);
        Assert.Equal((false, 1), (second, _sender.Calls));
    }

    private async Task EnqueueAsync(bool commit)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var transaction = await new IdentityTransactionParticipant(db).BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (transaction.ConfigureAwait(true))
        {
            scope.ServiceProvider.GetRequiredService<IInvitationEmailJobStore>().Enqueue(_tenantId, _invitationId, _userId,
                "invited@example.test", "Delivery", "secret-invitation-token", _clock.GetUtcNow().AddHours(24));
            if (commit) await transaction.CommitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            else
            {
                await db.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                await transaction.RollbackAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
        }
    }

    private sealed class ControlledSender : IInvitationEmailSender
    {
        internal int Calls;
        internal bool Fail;
        internal bool Hold;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendInvitationAsync(string recipientEmail, string tenantName, string invitationToken, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Started.TrySetResult();
            if (Hold) await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (Fail) throw new SmtpException("Simulated transport failure.");
        }
    }

    private sealed class DeliveryClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow.AddMinutes(1);
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan duration) => _now += duration;
    }
    private sealed record DeliveryTenantContext(Guid TenantId, Guid UserId) : ITenantContext
    {
        public bool IsAuthenticated => true;
    }
}
