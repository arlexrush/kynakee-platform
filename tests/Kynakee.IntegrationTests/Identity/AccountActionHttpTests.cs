using Kynakee.Api.Application.Abstractions;
using Kynakee.Api.Application.Behaviors;
using Kynakee.Api.Endpoints;
using Kynakee.Api.Infrastructure.Identity;
using Kynakee.Api.Infrastructure.Persistence;
using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Commands.AccountActions;
using Kynakee.Modules.Identity.Application.Commands.Login;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class AccountActionHttpTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private WebApplication _app = null!;
    private const string Address = "http-owner@example.test";
    private const string Password = "HttpInitial!Password123";
    private const string Replacement = "HttpReplacement!Pass456";

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        builder.Configuration["ConnectionStrings:Postgres"] = _postgres.GetConnectionString();
        builder.Configuration["Authentication:SigningKey"] = signingKey;
        builder.Configuration["Identity:InvitationEmail:ActiveKeyId"] = "v1";
        builder.Configuration["Identity:InvitationEmail:Keys:v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        builder.Services.AddScoped<ITenantContext>(_ => new AnonymousTenant());
        builder.Services.AddIdentityModule(builder.Configuration);
        builder.Services.AddSingleton(new AccountEmailPayloadProtector(builder.Configuration));
        builder.Services.AddScoped<IAccountEmailJobStore, AccountEmailJobStore>();
        builder.Services.AddSingleton<IHttpIdempotencyProtector>(new IdentityHttpIdempotencyProtector(signingKey));
        builder.Services.AddSingleton(new IdentityEntityTagProtector(signingKey));
        builder.Services.AddScoped<ITransactionManager, ModuleTransactionManager>();
        builder.Services.AddScoped<IPostCommitActionDispatcher, PostCommitActionDispatcher>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        builder.Services.AddScoped<IdentityJwtBearerEvents>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.EventsType = typeof(IdentityJwtBearerEvents);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "Kynakee", ValidateAudience = true, ValidAudience = "KynakeeClients",
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("identity-auth", limiter =>
        {
            limiter.PermitLimit = 100;
            limiter.Window = TimeSpan.FromMinutes(1);
        }));
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.MapIdentityEndpoints();
        _app.MapGet("/credential-check", () => Results.Ok()).RequireAuthorization();
        await _app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database
            .EnsureCreatedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(true);
        await _postgres.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task RegisterHttpReturnsAcceptedWithoutSession()
    {
        using var response = await PostAsync("register", Registration());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal((HttpStatusCode.Accepted, false), (response.StatusCode, body.RootElement.GetProperty("data").TryGetProperty("accessToken", out _)));
    }

    [Fact]
    public async Task RegisterHttpReplayCreatesOneVerificationJob()
    {
        var key = Guid.NewGuid().ToString("N");
        using var first = await PostAsync("register", Registration(), key);
        using var replay = await PostAsync("register", Registration(), key);
        using var scope = _app.Services.CreateScope();
        var count = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AccountEmailJobs.IgnoreQueryFilters()
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Accepted, HttpStatusCode.Accepted, 1), (first.StatusCode, replay.StatusCode, count));
    }

    [Fact]
    public async Task RegisterHttpChangedRequestWithSameKeyReturnsConflict()
    {
        var key = Guid.NewGuid().ToString("N");
        using var first = await PostAsync("register", Registration(), key);

        using var changed = await PostAsync("register", Registration() with { TenantName = "Changed" }, key);

        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task VerifyEmailHttpConsumedTokenReturnsUnauthorized()
    {
        using var registration = await PostAsync("register", Registration());
        var token = await ReadTokenAsync(AccountActionPurpose.VerifyEmail);
        using var first = await PostAsync("email/verify", new VerifyEmailCommand(token));

        using var replay = await PostAsync("email/verify", new VerifyEmailCommand(token));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Unauthorized), (first.StatusCode, replay.StatusCode));
    }

    [Fact]
    public async Task ResetPasswordHttpRejectsPreviousSignedAccessToken()
    {
        var access = await PrepareRecoveryAsync();
        using var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var before = await client.GetAsync(new Uri("/credential-check", UriKind.Relative), TestContext.Current.CancellationToken);
        var token = await ReadTokenAsync(AccountActionPurpose.ResetPassword);

        using var reset = await PostAsync("password/reset", new ResetPasswordCommand(token, Replacement));
        using var after = await client.GetAsync(new Uri("/credential-check", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Unauthorized), (before.StatusCode, reset.StatusCode, after.StatusCode));
    }

    [Fact]
    public async Task ResetPasswordHttpNewLoginIssuesUsableAccessToken()
    {
        await PrepareRecoveryAsync();
        using var reset = await PostAsync("password/reset", new ResetPasswordCommand(await ReadTokenAsync(AccountActionPurpose.ResetPassword), Replacement));
        var access = await LoginAsync(Replacement);
        using var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        using var response = await client.GetAsync(new Uri("/credential-check", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("email/resend")]
    [InlineData("password/forgot")]
    public async Task AccountEmailHttpReplayDoesNotEnqueueAdditionalJob(string route)
    {
        await PrepareRecoveryAsync();
        var key = Guid.NewGuid().ToString("N");
        using var first = await PostAsync(route, new { Email = Address }, key);

        using var replay = await PostAsync(route, new { Email = Address }, key);

        using var scope = _app.Services.CreateScope();
        var count = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AccountEmailJobs.IgnoreQueryFilters()
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.Accepted, HttpStatusCode.Accepted, 2), (first.StatusCode, replay.StatusCode, count));
    }

    [Fact]
    public async Task RegisterHttpConcurrentSameKeyCreatesOneAccountAndJob()
    {
        var key = Guid.NewGuid().ToString("N");

        var requests = await Task.WhenAll(PostAsync("register", Registration(), key), PostAsync("register", Registration(), key));
        using var second = requests[0];
        using var third = requests[1];

        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = await db.Users.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        var jobs = await db.AccountEmailJobs.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.Accepted, HttpStatusCode.Accepted, 1, 1), (second.StatusCode, third.StatusCode, users, jobs));
    }

    [Fact]
    public async Task RegisterHttpConcurrentDifferentKeysCreatesOneAccountAndJob()
    {
        var responses = await Task.WhenAll(PostAsync("register", Registration()), PostAsync("register", Registration()));
        using var first = responses[0];
        using var second = responses[1];

        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = await db.Users.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        var jobs = await db.AccountEmailJobs.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.Accepted, HttpStatusCode.Accepted, 1, 1), (first.StatusCode, second.StatusCode, users, jobs));
    }

    [Fact]
    public async Task VerifyEmailHttpConcurrentConsumptionActivatesOnce()
    {
        using var registration = await PostAsync("register", Registration());
        var token = await ReadTokenAsync(AccountActionPurpose.VerifyEmail);

        var responses = await Task.WhenAll(PostAsync("email/verify", new VerifyEmailCommand(token)),
            PostAsync("email/verify", new VerifyEmailCommand(token)));
        using var first = responses[0];
        using var second = responses[1];

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized },
            responses.Select(response => response.StatusCode).Order().ToArray());
    }

    [Fact]
    public async Task ResetPasswordHttpConcurrentConsumptionChangesCredentialsOnce()
    {
        await PrepareRecoveryAsync();
        var token = await ReadTokenAsync(AccountActionPurpose.ResetPassword);

        var responses = await Task.WhenAll(PostAsync("password/reset", new ResetPasswordCommand(token, Replacement)),
            PostAsync("password/reset", new ResetPasswordCommand(token, Replacement)));
        using var first = responses[0];
        using var second = responses[1];

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized },
            responses.Select(response => response.StatusCode).Order().ToArray());
    }

    [Fact]
    public async Task ResetPasswordHttpConcurrentRefreshLeavesNoUsableOldRefresh()
    {
        await PrepareRecoveryAsync();
        using var login = await PostAsync("login", new LoginCommand(Address, Password, "web"));
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var refresh = body.RootElement.GetProperty("data").GetProperty("refreshToken").GetString()!;
        var token = await ReadTokenAsync(AccountActionPurpose.ResetPassword);

        var responses = await Task.WhenAll(PostAsync("password/reset", new ResetPasswordCommand(token, Replacement)),
            PostAsync("refresh", new { RefreshToken = refresh }));
        using var reset = responses[0];
        using var rotated = responses[1];

        using var scope = _app.Services.CreateScope();
        var usable = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().RefreshTokens.IgnoreQueryFilters()
            .CountAsync(candidate => candidate.RevokedAt == null, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.OK, 0), (reset.StatusCode, usable));
    }

    [Fact]
    public async Task ResetPasswordHttpWeakPasswordDoesNotConsumeCredential()
    {
        await PrepareRecoveryAsync();
        var token = await ReadTokenAsync(AccountActionPurpose.ResetPassword);
        using var rejected = await PostAsync("password/reset", new ResetPasswordCommand(token, "weak"));

        using var valid = await PostAsync("password/reset", new ResetPasswordCommand(token, Replacement));

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.OK), (rejected.StatusCode, valid.StatusCode));
    }

    [Theory]
    [InlineData("email/resend")]
    [InlineData("password/forgot")]
    public async Task AccountEmailHttpChangedAddressWithSameKeyReturnsConflict(string route)
    {
        var key = Guid.NewGuid().ToString("N");
        using var first = await PostAsync(route, new { Email = Address }, key);

        using var second = await PostAsync(route, new { Email = "different@example.test" }, key);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private async Task<string> PrepareRecoveryAsync()
    {
        using var registration = await PostAsync("register", Registration()).ConfigureAwait(true);
        var token = await ReadTokenAsync(AccountActionPurpose.VerifyEmail).ConfigureAwait(true);
        using var confirmation = await PostAsync("email/verify", new VerifyEmailCommand(token)).ConfigureAwait(true);
        var access = await LoginAsync(Password).ConfigureAwait(true);
        using var forgot = await PostAsync("password/forgot", new RequestPasswordResetCommand(Address)).ConfigureAwait(true);
        return access;
    }

    private async Task<string> LoginAsync(string password)
    {
        using var response = await PostAsync("login", new LoginCommand(Address, password, "web")).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        return body.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private async Task<HttpResponseMessage> PostAsync<T>(string route, T body, string? key = null)
    {
        using var client = _app.GetTestClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/" + route)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private async Task<string> ReadTokenAsync(AccountActionPurpose purpose)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var token = await db.AccountActionTokens.IgnoreQueryFilters().SingleAsync(action => action.Purpose == purpose,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        var job = await db.AccountEmailJobs.IgnoreQueryFilters().SingleAsync(mail => mail.TokenId == token.Id,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        return scope.ServiceProvider.GetRequiredService<AccountEmailPayloadProtector>()
            .Unprotect(token.TenantId, token.Id, job.EncryptedPayload).Token;
    }

    private static RegisterTenantOwnerCommand Registration() => new("Ada", "Lovelace", Address, Password,
        "+34612345678", "HTTP Account Workflows", TenantType.Company, "GB123456789", "GB", null, null, null,
        null, null, null, "starter", "web");

    private sealed record AnonymousTenant : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
    }
}
