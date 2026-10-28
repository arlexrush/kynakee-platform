using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Commands.InviteUser;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class IdentityReplaySecurityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly ReplayClock _clock = new();
    private ServiceProvider _provider = null!;
    private Guid _tenantId;
    private Guid _userId;
    private Guid _invitationId;
    private IdentitySessionResponse _session = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _postgres.StartAsync(ct).ConfigureAwait(true);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TestHostEnvironment.Create());
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<ITenantContext>(new AnonymousTenantContext());
        services.AddIdentityModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString()
        }).Build());
        _provider = services.BuildServiceProvider();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(true);
        var tenant = Tenant.Create("Replay", TenantSlug.Create("replay").Value, TenantType.Company, null, null, "starter").Value!;
        var user = User.Create(tenant.Id, "Replay", "Owner", Email.Create("owner@example.test").Value,
            null, "hashed-password", UserStatus.Active).Value!;
        var member = TenantUser.Create(user, UserRole.Owner).Value!;
        const string rawRefresh = "stored-refresh";
        var token = RefreshToken.Create(tenant.Id, user.Id, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefresh))), _clock.GetUtcNow()).Value!;
        var invitation = UserInvitation.Create(tenant.Id, user.Id, new string('A', 64), _clock.GetUtcNow()).Value!;
        db.AddRange(tenant, user, member, token, invitation);
        await db.SaveChangesAsync(ct).ConfigureAwait(true);
        _tenantId = tenant.Id;
        _userId = user.Id;
        _invitationId = invitation.Id;
        var jwt = new JwtSecurityToken(claims: [new Claim("sub", user.Id.ToString()), new Claim("tenant_id", tenant.Id.ToString())],
            expires: _clock.GetUtcNow().AddMinutes(15).UtcDateTime);
        _session = new IdentitySessionResponse(new JwtSecurityTokenHandler().WriteToken(jwt), rawRefresh, 900, "Bearer",
            new IdentityUserProfile(user.Id, tenant.Id, user.FirstName, user.LastName, user.Email.Value, null, UserRole.Owner, UserStatus.Active),
            new IdentityTenantProfile(tenant.Id, tenant.Name, tenant.Slug.Value, tenant.Type, tenant.PlanId, tenant.Status,
                null, null, new IdentityCompanySettings(0, 0, 0, 0, 0, 0), new IdentityCompanyBranding(null, null, null, null)));
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync().ConfigureAwait(true);
        await _postgres.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task SessionReplayActiveIdentityShouldBeAllowed()
    {
        var error = await ValidateSessionAsync();
        Assert.Null(error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TokenConsumptionConcurrentTransactionsShouldReadCommittedRevocation(bool invitation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        using var firstScope = _provider.CreateScope();
        using var secondScope = _provider.CreateScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IIdentityRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IIdentityRepository>();
        var firstTransaction = await new IdentityTransactionParticipant(firstDb, _clock).BeginTransactionAsync(ct).ConfigureAwait(true);
        var secondTransaction = await new IdentityTransactionParticipant(secondDb, _clock).BeginTransactionAsync(ct).ConfigureAwait(true);
        await using (firstTransaction.ConfigureAwait(true))
        await using (secondTransaction.ConfigureAwait(true))
        {
            var hash = invitation ? new string('A', 64) : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("stored-refresh")));
            if (invitation)
            {
                var first = await firstRepository.FindInvitationByHashAsync(hash, ct);
                first!.Revoke(_clock.GetUtcNow());
                var waiting = secondRepository.FindInvitationByHashAsync(hash, ct);
                var delay = Task.Delay(150, ct);
                Assert.Same(delay, await Task.WhenAny(waiting, delay));
                await firstTransaction.CommitAsync(ct);
                var second = await waiting.ConfigureAwait(true);
                Assert.False(second!.IsUsableAt(_clock.GetUtcNow()));
            }
            else
            {
                var first = await firstRepository.FindRefreshTokenByHashAsync(hash, ct);
                first!.Revoke(_clock.GetUtcNow());
                var waiting = secondRepository.FindRefreshTokenByHashAsync(hash, ct);
                var delay = Task.Delay(150, ct);
                Assert.Same(delay, await Task.WhenAny(waiting, delay));
                await firstTransaction.CommitAsync(ct);
                var second = await waiting.ConfigureAwait(true);
                Assert.False(second!.IsUsableAt(_clock.GetUtcNow()));
            }
        }
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("replaced")]
    [InlineData("inactive")]
    [InlineData("tenantDeleted")]
    [InlineData("membershipDeleted")]
    public async Task SessionReplayInvalidSecurityStateShouldRejectStoredSession(string state)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        switch (state)
        {
            case "revoked":
                (await db.RefreshTokens.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken)).Revoke(_clock.GetUtcNow());
                break;
            case "replaced":
                (await db.RefreshTokens.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken)).Revoke(_clock.GetUtcNow(), Guid.NewGuid());
                break;
            case "inactive":
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE schema_identity.users SET status = 'Inactive' WHERE id = {_userId}", TestContext.Current.CancellationToken);
                break;
            case "tenantDeleted":
                (await db.Tenants.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken)).Delete(_userId);
                break;
            default:
                (await db.TenantUsers.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken)).Delete(_userId);
                break;
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var error = await ValidateSessionAsync();

        Assert.Equal("IDENTITY_REPLAY_SESSION_UNAVAILABLE", error?.Code);
    }

    [Fact]
    public async Task SessionReplayExpiredAccessShouldRejectOutcomeWithoutNewTokens()
    {
        _clock.Advance(TimeSpan.FromMinutes(16));
        var error = await ValidateSessionAsync();
        Assert.Equal("IDENTITY_IDEMPOTENCY_OUTCOME_UNAVAILABLE", error?.Code);
    }

    [Fact]
    public async Task SessionReplayExpiredRefreshShouldRejectSession()
    {
        _clock.Advance(TimeSpan.FromDays(31));
        var error = await ValidateSessionAsync();
        Assert.Equal("IDENTITY_REPLAY_SESSION_UNAVAILABLE", error?.Code);
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("revoked")]
    [InlineData("expired")]
    public async Task InvitationReplayUnavailableInvitationShouldReturnConflict(string state)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var invitation = await db.UserInvitations.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        switch (state)
        {
            case "accepted": invitation.Accept(_clock.GetUtcNow()); break;
            case "revoked": invitation.Revoke(_clock.GetUtcNow()); break;
            default: _clock.Advance(TimeSpan.FromHours(25)); break;
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var error = await ValidateInvitationAsync();

        Assert.Equal("IDENTITY_REPLAY_INVITATION_UNAVAILABLE", error?.Code);
    }

    [Fact]
    public async Task InvitationReplayPermissionLostShouldBeForbidden()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await db.TenantUsers.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken)).ChangeRole(UserRole.Viewer, _userId);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var error = await ValidateInvitationAsync();

        Assert.Equal("IDENTITY_INVITATION_FORBIDDEN", error?.Code);
    }

    [Fact]
    public async Task ExpiredIdempotencyRecordShouldRetainClaimAfterOutcomePurge()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var request = new IdempotencyRequest(new string('B', 64), new string('C', 64), Guid.Empty, Guid.Empty, "identity.login");
        var record = IdentityIdempotencyRecord.Create(Guid.Empty, request.KeyHash, request.Fingerprint, [1, 2, 3], _clock.GetUtcNow().AddHours(-1));
        db.IdempotencyRecords.Add(record);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await new IdentityIdempotencyMaintenance(db, _clock).PurgeExpiredOutcomesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var payload = await db.IdempotencyRecords.IgnoreQueryFilters()
            .Select(item => EF.Property<byte[]>(item, "_encryptedOutcome")).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Empty(payload);
        var transaction = await new IdentityTransactionParticipant(db, _clock).BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (transaction.ConfigureAwait(true))
        {
            var result = await transaction.Idempotency!.AcquireAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal((true, true, true, true), (result.Exists, result.FingerprintMatches, result.Expired, result.EncryptedOutcome is null));
        }
    }

    private async Task<ApplicationError?> ValidateSessionAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var transaction = await new IdentityTransactionParticipant(db, _clock).BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (transaction.ConfigureAwait(true))
        {
            return await transaction.Idempotency!.ValidateReplayAsync(new object(), ResultFactory.Success(_session), TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }

    private async Task<ApplicationError?> ValidateInvitationAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var transaction = await new IdentityTransactionParticipant(db, _clock).BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (transaction.ConfigureAwait(true))
        {
            return await transaction.Idempotency!.ValidateReplayAsync(
                new InviteUserCommand(_tenantId, _userId, "Invite", "User", "invite@example.test", null, UserRole.Viewer),
                ResultFactory.Success(new InvitationCreatedResponse(_invitationId, _clock.GetUtcNow().AddHours(24))), TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }

    private sealed class ReplayClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class AnonymousTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
    }
}
