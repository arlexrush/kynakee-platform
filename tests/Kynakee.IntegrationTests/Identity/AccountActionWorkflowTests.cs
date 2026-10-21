using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Commands.AccountActions;
using Kynakee.Modules.Identity.Application.Commands.Login;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class AccountActionWorkflowTests : IAsyncLifetime
{
    private const string Password = "InitialStrong!Pass123";
    private const string Replacement = "ReplacementStrong!456";
    private const string Address = "owner@example.test";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly WorkflowClock _clock = new();
    private readonly ControlledAccountSender _sender = new();
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["Authentication:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            ["Identity:InvitationEmail:ActiveKeyId"] = "v1",
            ["Identity:InvitationEmail:Keys:v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ITenantContext>(_ => new AnonymousTenant());
        services.AddIdentityModule(configuration);
        services.AddSingleton(new AccountEmailPayloadProtector(configuration));
        services.AddScoped<IAccountEmailJobStore, AccountEmailJobStore>();
        services.AddSingleton<IAccountEmailSender>(_sender);
        services.AddScoped<AccountEmailJobProcessor>();
        _provider = services.BuildServiceProvider();
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database
            .EnsureCreatedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync().ConfigureAwait(true);
        await _postgres.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task RegisterNewOwnerPersistsPendingWithoutRefreshToken()
    {
        await SendAsync(Registration());
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        var refreshCount = await db.RefreshTokens.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        var jobs = await db.AccountEmailJobs.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal((UserStatus.PendingVerification, 0, 1), (user.Status, refreshCount, jobs));
    }

    [Fact]
    public async Task LoginPendingOwnerRejectsCredentials()
    {
        await SendAsync(Registration());

        var result = await SendAsync(new LoginCommand(Address, Password, "web"));

        Assert.Equal("IDENTITY_CREDENTIALS_INVALID", result.Error?.Code);
    }

    [Fact]
    public async Task VerifyEmailValidCredentialAllowsLogin()
    {
        await SendAsync(Registration());
        var token = await ReadTokenAsync(AccountActionPurpose.VerifyEmail);
        await SendAsync(new VerifyEmailCommand(token));

        var result = await SendAsync(new LoginCommand(Address, Password, "web"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task VerifyEmailConsumedCredentialRejectsSecondConfirmation()
    {
        await SendAsync(Registration());
        var token = await ReadTokenAsync(AccountActionPurpose.VerifyEmail);
        await SendAsync(new VerifyEmailCommand(token));

        var result = await SendAsync(new VerifyEmailCommand(token));

        Assert.Equal("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", result.Error?.Code);
    }

    [Fact]
    public async Task ResetPasswordVerificationCredentialRejectsWrongPurpose()
    {
        await SendAsync(Registration());
        var token = await ReadTokenAsync(AccountActionPurpose.VerifyEmail);

        var result = await SendAsync(new ResetPasswordCommand(token, Replacement));

        Assert.Equal("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", result.Error?.Code);
    }

    [Fact]
    public async Task RegisterExistingAddressDoesNotCreateAdditionalTenantOrMail()
    {
        await SendAsync(Registration());

        await SendAsync(Registration() with { TenantName = "Another company", Password = Replacement });

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var tenants = await db.Tenants.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        var jobs = await db.AccountEmailJobs.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal((1, 1), (tenants, jobs));
    }

    [Fact]
    public async Task ResendVerificationWithinCooldownDoesNotEnqueueAdditionalMail()
    {
        await SendAsync(Registration());

        await SendAsync(new ResendVerificationCommand(Address));

        using var scope = _provider.CreateScope();
        var count = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AccountEmailJobs
            .IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RequestPasswordResetUnknownAddressReturnsGenericAcknowledgement()
    {
        var result = await SendAsync(new RequestPasswordResetCommand("unknown@example.test"));

        Assert.Equal(AccountActionAcknowledgement.Pending, result.Value);
    }

    [Fact]
    public async Task ResetPasswordActiveOwnerAcceptsReplacementPassword()
    {
        var token = await PrepareResetAsync();

        await SendAsync(new ResetPasswordCommand(token, Replacement));
        var result = await SendAsync(new LoginCommand(Address, Replacement, "web"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ResetPasswordActiveOwnerRevokesExistingRefreshTokens()
    {
        var token = await PrepareResetAsync();

        await SendAsync(new ResetPasswordCommand(token, Replacement));

        using var scope = _provider.CreateScope();
        var refresh = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().RefreshTokens
            .IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(refresh.RevokedAt);
    }

    [Fact]
    public async Task ResetPasswordActiveOwnerRejectsPreviousPassword()
    {
        var token = await PrepareResetAsync();

        await SendAsync(new ResetPasswordCommand(token, Replacement));
        var result = await SendAsync(new LoginCommand(Address, Password, "web"));

        Assert.Equal("IDENTITY_CREDENTIALS_INVALID", result.Error?.Code);
    }

    [Fact]
    public async Task RegisterRolledBackTransactionLeavesNoAccountOrMail()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using var disposal = transaction.ConfigureAwait(true);
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Registration(), TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
        using var verify = _provider.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var accounts = await context.Users.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        var jobs = await context.AccountEmailJobs.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal((0, 0), (accounts, jobs));
    }

    [Fact]
    public async Task ResetPasswordPreviousAccessCredentialVersionIsRejected()
    {
        var token = await PrepareResetAsync();
        Guid tenantId;
        Guid userId;
        Guid version;
        using (var scope = _provider.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
                .SingleAsync(TestContext.Current.CancellationToken);
            (tenantId, userId, version) = (user.TenantId, user.Id, user.CredentialVersion);
        }

        await SendAsync(new ResetPasswordCommand(token, Replacement));

        using var verify = _provider.CreateScope();
        var accepted = await verify.ServiceProvider.GetRequiredService<IIdentityAccessTokenValidator>()
            .IsCurrentAsync(tenantId, userId, version, nameof(UserRole.Owner), TestContext.Current.CancellationToken);
        Assert.False(accepted);
    }

    [Fact]
    public async Task AccessCredentialCurrentActiveOwnerIsAccepted()
    {
        await PrepareResetAsync();
        using var scope = _provider.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
            .SingleAsync(TestContext.Current.CancellationToken);

        var accepted = await scope.ServiceProvider.GetRequiredService<IIdentityAccessTokenValidator>()
            .IsCurrentAsync(user.TenantId, user.Id, user.CredentialVersion, nameof(UserRole.Owner), TestContext.Current.CancellationToken);

        Assert.True(accepted);
    }

    [Fact]
    public async Task AccountEmailWorkerCommittedJobDeliversAndErasesPayload()
    {
        await SendAsync(Registration());

        using (var scope = _provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AccountEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);

        using var verify = _provider.CreateScope();
        var job = await verify.ServiceProvider.GetRequiredService<IdentityDbContext>().AccountEmailJobs.IgnoreQueryFilters()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((1, true, 0), (_sender.Calls, job.CompletedAt is not null, job.EncryptedPayload.Length));
    }

    [Fact]
    public async Task AccountEmailWorkerConsumedVerificationDoesNotSend()
    {
        await SendAsync(Registration());
        await SendAsync(new VerifyEmailCommand(await ReadTokenAsync(AccountActionPurpose.VerifyEmail)));

        using (var scope = _provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AccountEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _sender.Calls);
    }

    [Fact]
    public async Task AccountEmailWorkerTransportFailureRetriesAndThenDelivers()
    {
        await SendAsync(Registration());
        _sender.Fail = true;
        using (var scope = _provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AccountEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);
        _sender.Fail = false;
        _clock.Advance(TimeSpan.FromMinutes(1));

        using (var scope = _provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AccountEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);

        using var verify = _provider.CreateScope();
        var job = await verify.ServiceProvider.GetRequiredService<IdentityDbContext>().AccountEmailJobs.IgnoreQueryFilters()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((2, true, 0), (_sender.Calls, job.CompletedAt is not null, job.EncryptedPayload.Length));
    }

    [Fact]
    public async Task ResendVerificationAfterCooldownRevokesPreviousCredential()
    {
        await SendAsync(Registration());
        var previous = await ReadTokenAsync(AccountActionPurpose.VerifyEmail);
        _clock.Advance(TimeSpan.FromMinutes(2));
        await SendAsync(new ResendVerificationCommand(Address));

        var result = await SendAsync(new VerifyEmailCommand(previous));

        Assert.Equal("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", result.Error?.Code);
    }

    [Fact]
    public async Task VerifyEmailExpiredCredentialCannotActivateOwner()
    {
        await SendAsync(Registration());
        var token = await ReadTokenAsync(AccountActionPurpose.VerifyEmail);
        _clock.Advance(TimeSpan.FromHours(24));

        var result = await SendAsync(new VerifyEmailCommand(token));

        Assert.Equal("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", result.Error?.Code);
    }

    [Fact]
    public async Task ResetPasswordExpiredCredentialCannotChangePassword()
    {
        var token = await PrepareResetAsync();
        _clock.Advance(TimeSpan.FromMinutes(30));

        var result = await SendAsync(new ResetPasswordCommand(token, Replacement));

        Assert.Equal("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", result.Error?.Code);
    }

    [Fact]
    public async Task AccountEmailWorkerInvalidPayloadIsPurgedWithoutDelivery()
    {
        await SendAsync(Registration());
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var action = await db.AccountActionTokens.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
            var job = await db.AccountEmailJobs.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
            var invalid = scope.ServiceProvider.GetRequiredService<AccountEmailPayloadProtector>()
                .Protect(action.TenantId, action.Id, new AccountEmailPayload(Address, null!, AccountActionPurpose.VerifyEmail));
            db.Entry(job).Property(candidate => candidate.EncryptedPayload).CurrentValue = invalid;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var scope = _provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AccountEmailJobProcessor>().ProcessNextAsync(TestContext.Current.CancellationToken);

        using var verify = _provider.CreateScope();
        var result = await verify.ServiceProvider.GetRequiredService<IdentityDbContext>().AccountEmailJobs.IgnoreQueryFilters()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((0, "PAYLOAD_INVALID", 0), (_sender.Calls, result.FailureCode, result.EncryptedPayload.Length));
    }

    [Fact]
    public async Task ResetPasswordDisabledOwnerCannotBeReactivated()
    {
        var token = await PrepareResetAsync();
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
            user.Deactivate();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await SendAsync(new ResetPasswordCommand(token, Replacement));

        Assert.Equal("IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", result.Error?.Code);
    }

    [Fact]
    public async Task AccountEmailWorkerConcurrentClaimSkipsLockedJob()
    {
        await SendAsync(Registration());
        _sender.Hold = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var firstScope = _provider.CreateScope();
        using var secondScope = _provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<AccountEmailJobProcessor>().ProcessNextAsync(timeout.Token);
        await _sender.Started.Task.WaitAsync(timeout.Token);
        var second = await secondScope.ServiceProvider.GetRequiredService<AccountEmailJobProcessor>().ProcessNextAsync(timeout.Token);
        _sender.Release.TrySetResult();
        await first.ConfigureAwait(true);

        Assert.Equal((false, 1), (second, _sender.Calls));
    }

    private async Task<string> PrepareResetAsync()
    {
        await SendAsync(Registration()).ConfigureAwait(true);
        var verification = await ReadTokenAsync(AccountActionPurpose.VerifyEmail).ConfigureAwait(true);
        await SendAsync(new VerifyEmailCommand(verification)).ConfigureAwait(true);
        await SendAsync(new LoginCommand(Address, Password, "web")).ConfigureAwait(true);
        await SendAsync(new RequestPasswordResetCommand(Address)).ConfigureAwait(true);
        return await ReadTokenAsync(AccountActionPurpose.ResetPassword).ConfigureAwait(true);
    }

    private async Task<Result<T>> SendAsync<T>(IRequest<Result<T>> command)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var disposal = transaction.ConfigureAwait(true);
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, TestContext.Current.CancellationToken).ConfigureAwait(true);
        if (result.IsSuccess)
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await transaction.CommitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        return result;
    }

    private async Task<string> ReadTokenAsync(AccountActionPurpose purpose)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var token = await db.AccountActionTokens.IgnoreQueryFilters().SingleAsync(action => action.Purpose == purpose,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        var job = await db.AccountEmailJobs.IgnoreQueryFilters().SingleAsync(mail => mail.TokenId == token.Id,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        return scope.ServiceProvider.GetRequiredService<AccountEmailPayloadProtector>()
            .Unprotect(token.TenantId, token.Id, job.EncryptedPayload).Token;
    }

    private static RegisterTenantOwnerCommand Registration() => new("Ada", "Lovelace", Address, Password,
        "+34612345678", "Account Workflows", TenantType.Company, "GB123456789", "GB", null, null, null,
        null, null, null, "starter", "web");

    private sealed class WorkflowClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class ControlledAccountSender : IAccountEmailSender
    {
        internal int Calls;
        internal bool Fail;
        internal bool Hold;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendAsync(string recipient, string token, AccountActionPurpose purpose, DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref Calls);
            Started.TrySetResult();
            if (Hold) await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (Fail) throw new System.Net.Mail.SmtpException("Simulated transport failure.");
        }
    }

    private sealed record AnonymousTenant : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
    }
}
