using Kynakee.Api.Application.Abstractions;
using Kynakee.Api.Application.Behaviors;
using Kynakee.Api.Endpoints;
using Kynakee.Api.Infrastructure.ExceptionHandling;
using Kynakee.Api.Infrastructure.Identity;
using Kynakee.Api.Infrastructure.Persistence;
using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Kynakee.Modules.Identity.Application.Commands.RevokeSession;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Identity;

public sealed class IdentityHttpPreconditionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly IdentityEntityTagProtector _protector = new(new string('e', 32));
    private readonly ConcurrentSaveGate _saveGate = new();
    private WebApplication _app = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _postgres.StartAsync(ct).ConfigureAwait(true);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["ConnectionStrings:Postgres"] = _postgres.GetConnectionString();
        builder.Configuration["Authentication:SigningKey"] = new string('s', 32);
        builder.Configuration["Identity:InvitationEmail:ActiveKeyId"] = "v1";
        builder.Configuration["Identity:InvitationEmail:Keys:v1"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("identity-auth", limiter =>
        {
            limiter.PermitLimit = 100;
            limiter.Window = TimeSpan.FromMinutes(1);
        }));
        builder.Services.AddScoped<ITenantContext>(_ => new TestTenantContext(_seedTenantId, _seedUserId));
        builder.Services.AddIdentityModule(builder.Configuration);
        builder.Services.AddSingleton(new InvitationEmailPayloadProtector(builder.Configuration));
        builder.Services.AddScoped<IInvitationEmailJobStore, InvitationEmailJobStore>();
        builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(_saveGate));
        builder.Services.AddSingleton(_protector);
        builder.Services.AddSingleton<IHttpIdempotencyProtector>(new IdentityHttpIdempotencyProtector(new string('i', 32)));
        builder.Services.AddScoped<ITransactionManager, ModuleTransactionManager>();
        builder.Services.AddScoped<IPostCommitActionDispatcher, PostCommitActionDispatcher>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<IdentityUniqueConstraintExceptionHandler>();
        _app = builder.Build();
        _app.UseExceptionHandler();
        _app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant_id", _seedTenantId.ToString()), new Claim("sub", _seedUserId.ToString())], "Test"));
            await next(context).ConfigureAwait(false);
        });
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.MapIdentityEndpoints();
        await _app.StartAsync(ct).ConfigureAwait(true);
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(true);
        var tenant = Tenant.Create("Tenant", TenantSlug.Create("preconditions").Value, TenantType.Company, null, null, "starter").Value!;
        _seedTenantId = tenant.Id;
        var owner = User.Create(tenant.Id, "First", "Owner", Email.Create("owner@example.test").Value,
            null, null, UserStatus.PendingVerification).Value!;
        var target = User.Create(tenant.Id, "Second", "Member", Email.Create("member@example.test").Value,
            null, "hashed-password", UserStatus.Active).Value!;
        var passwordResult = await scope.ServiceProvider.GetRequiredService<UserManager<User>>().AddPasswordAsync(owner, "Password!12345").ConfigureAwait(true);
        Assert.True(passwordResult.Succeeded);
        Assert.True(owner.AcceptInvitation(owner.PasswordHash).IsSuccess);
        _seedUserId = owner.Id;
        _seedTargetId = target.Id;
        db.AddRange(tenant, owner, target, TenantUser.Create(owner, UserRole.Owner).Value!, TenantUser.Create(target, UserRole.Admin).Value!);
        await db.SaveChangesAsync(ct).ConfigureAwait(true);
    }

    private Guid _seedTenantId;
    private Guid _seedUserId;
    private Guid _seedTargetId;

    [Fact]
    public async Task InviteReplayShouldPersistOneInvitationAndOneEncryptedEmailJob()
    {
        using var client = _app.GetTestClient();
        using var first = CreateInviteRequest();
        using var second = CreateInviteRequest();
        using var firstResponse = await client.SendAsync(first, TestContext.Current.CancellationToken);
        firstResponse.EnsureSuccessStatusCode();
        using var secondResponse = await client.SendAsync(second, TestContext.Current.CancellationToken);
        secondResponse.EnsureSuccessStatusCode();
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var invitations = await db.UserInvitations.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        var jobs = await db.InvitationEmailJobs.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal((1, 1), (invitations, jobs));
    }

    private static HttpRequestMessage CreateInviteRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/users/invite")
        {
            Content = new StringContent("{\"firstName\":\"Invited\",\"lastName\":\"User\",\"email\":\"new@example.test\",\"phone\":null,\"role\":2}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", "0123456789abcdefghijkl");
        return request;
    }

    [Fact]
    public async Task LoginReplayRevokedSessionShouldReturnUnauthorizedWithoutNewSession()
    {
        using var client = _app.GetTestClient();
        const string key = "0123456789abcdefghijkl";
        using var first = await LoginAsync(client, key);
        first.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var refresh = json.RootElement.GetProperty("data").GetProperty("refreshToken").GetString()!;
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RevokeSessionCommand(refresh), TestContext.Current.CancellationToken);

        using var response = await LoginAsync(client, key);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginReplaySameRequestShouldReturnSameSession()
    {
        using var client = _app.GetTestClient();
        const string key = "0123456789abcdefghijkl";
        using var first = await LoginAsync(client, key);
        first.EnsureSuccessStatusCode();
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        using var second = await LoginAsync(client, key);
        second.EnsureSuccessStatusCode();
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(firstJson.RootElement.GetProperty("data").GetProperty("refreshToken").GetString(),
            secondJson.RootElement.GetProperty("data").GetProperty("refreshToken").GetString());
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/login")
        {
            Content = new StringContent("{\"email\":\"owner@example.test\",\"password\":\"Password!12345\",\"channel\":\"web\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task LoginExpiredClaimShouldReturnConflictWithoutAdditionalTokens()
    {
        using var client = _app.GetTestClient();
        const string key = "0123456789abcdefghijkl";
        using var first = await LoginAsync(client, key);
        first.EnsureSuccessStatusCode();
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE schema_identity.idempotency_records SET expires_at = {DateTimeOffset.UtcNow.AddHours(-1)}",
            TestContext.Current.CancellationToken);

        using var replay = await LoginAsync(client, key);
        var count = await db.RefreshTokens.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Conflict, 1), (replay.StatusCode, count));
    }

    [Fact]
    public async Task LoginDifferentRequestSameKeyShouldReturnConflictWithoutAdditionalTokens()
    {
        using var client = _app.GetTestClient();
        const string key = "0123456789abcdefghijkl";
        using var first = await LoginAsync(client, key);
        first.EnsureSuccessStatusCode();
        using var changed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/login")
        {
            Content = new StringContent("{\"email\":\"owner@example.test\",\"password\":\"Changed!12345\",\"channel\":\"web\"}", Encoding.UTF8, "application/json")
        };
        changed.Headers.Add("Idempotency-Key", key);

        using var response = await client.SendAsync(changed, TestContext.Current.CancellationToken);
        using var scope = _app.Services.CreateScope();
        var count = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().RefreshTokens.IgnoreQueryFilters()
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Conflict, 1), (response.StatusCode, count));
    }

    [Fact]
    public async Task LogoutRepeatedTokenShouldReturnSuccess()
    {
        using var client = _app.GetTestClient();
        using var login = await LoginAsync(client, "0123456789abcdefghijkl");
        login.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var refresh = json.RootElement.GetProperty("data").GetProperty("refreshToken").GetString()!;
        using var scope = _app.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var command = new RevokeSessionCommand(refresh);
        var first = await sender.Send(command, TestContext.Current.CancellationToken);

        var second = await sender.Send(command, TestContext.Current.CancellationToken);

        Assert.Equal((true, true), (first.IsSuccess, second.IsSuccess));
    }

    [Fact]
    public async Task ConcurrentRefreshDifferentKeysShouldCreateOneReplacement()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var client = _app.GetTestClient();
        using var login = await LoginAsync(client, "0123456789abcdefghijkl");
        login.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync(timeout.Token));
        var refresh = json.RootElement.GetProperty("data").GetProperty("refreshToken").GetString()!;
        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/refresh")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { refreshToken = refresh }), Encoding.UTF8, "application/json")
        };
        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/refresh")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { refreshToken = refresh }), Encoding.UTF8, "application/json")
        };
        first.Headers.Add("Idempotency-Key", "1123456789abcdefghijkl");
        second.Headers.Add("Idempotency-Key", "2123456789abcdefghijkl");

        var responses = await Task.WhenAll(client.SendAsync(first, timeout.Token), client.SendAsync(second, timeout.Token));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        using var scope = _app.Services.CreateScope();
        var count = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().RefreshTokens.IgnoreQueryFilters()
            .CountAsync(timeout.Token);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Unauthorized, 2),
            (responses.Min(response => response.StatusCode), responses.Max(response => response.StatusCode), count));
    }

    [Fact]
    public async Task ConcurrentInvitationAcceptanceDifferentKeysShouldCreateOneSession()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = User.Create(_seedTenantId, "Invited", "User", Email.Create("invited@example.test").Value,
            null, null, UserStatus.PendingVerification).Value!;
        var raw = "invitation-to-accept";
        var tokens = scope.ServiceProvider.GetRequiredService<Kynakee.Modules.Identity.Application.Contracts.IIdentityTokenService>();
        db.AddRange(user, TenantUser.Create(user, UserRole.Viewer).Value!,
            UserInvitation.Create(_seedTenantId, user.Id, tokens.HashToken(raw), DateTimeOffset.UtcNow).Value!);
        await db.SaveChangesAsync(timeout.Token);
        using var client = _app.GetTestClient();
        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/invitations/accept")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { invitationToken = raw, password = "Password!12345" }), Encoding.UTF8, "application/json")
        };
        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/invitations/accept")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { invitationToken = raw, password = "Password!12345" }), Encoding.UTF8, "application/json")
        };
        first.Headers.Add("Idempotency-Key", "1123456789abcdefghijkl");
        second.Headers.Add("Idempotency-Key", "2123456789abcdefghijkl");

        var responses = await Task.WhenAll(client.SendAsync(first, timeout.Token), client.SendAsync(second, timeout.Token));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        var count = await db.RefreshTokens.IgnoreQueryFilters().CountAsync(timeout.Token);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Unauthorized, 1),
            (responses.Min(response => response.StatusCode), responses.Max(response => response.StatusCode), count));
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(true);
        await _postgres.DisposeAsync().ConfigureAwait(true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConditionalMutationMissingHeaderShouldReturnPreconditionRequired(int operation)
    {
        using var client = _app.GetTestClient();
        using var request = CreateRequest(operation);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConditionalMutationOldVersionShouldReturnPreconditionFailed(int operation)
    {
        using var client = _app.GetTestClient();
        using var request = CreateRequest(operation);
        request.Headers.TryAddWithoutValidation("If-Match", CreateTag(operation, 0));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConditionalMutationCurrentVersionShouldSucceed(int operation)
    {
        using var client = _app.GetTestClient();
        var tag = await ReadTagAsync(client, operation);
        using var request = CreateRequest(operation);
        request.Headers.TryAddWithoutValidation("If-Match", tag);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private string CreateTag(int operation, uint version) => operation switch
    {
        0 => _protector.Create(_seedTenantId, IdentityEntityTagResource.User, _seedUserId, version),
        1 or 2 => _protector.Create(_seedTenantId, IdentityEntityTagResource.Tenant, _seedTenantId, version),
        _ => _protector.Create(_seedTenantId, IdentityEntityTagResource.Membership, _seedTargetId, version)
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConditionalMutationOtherTenantTagShouldReturnPreconditionFailed(int operation)
    {
        using var client = _app.GetTestClient();
        using var request = CreateRequest(operation);
        var type = operation switch { 0 => IdentityEntityTagResource.User, 1 or 2 => IdentityEntityTagResource.Tenant, _ => IdentityEntityTagResource.Membership };
        var resource = operation switch { 0 => _seedUserId, 1 or 2 => _seedTenantId, _ => _seedTargetId };
        request.Headers.TryAddWithoutValidation("If-Match", _protector.Create(Guid.NewGuid(),
            type, resource, 1));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConditionalMutationReusedVersionShouldPreservePreconditionOrDeletedResourceSemantics(int operation)
    {
        using var client = _app.GetTestClient();
        var tag = await ReadTagAsync(client, operation);
        using var first = CreateRequest(operation);
        first.Headers.TryAddWithoutValidation("If-Match", tag);
        using var firstResponse = await client.SendAsync(first, TestContext.Current.CancellationToken);
        firstResponse.EnsureSuccessStatusCode();
        using var second = CreateRequest(operation);
        second.Headers.TryAddWithoutValidation("If-Match", tag);

        using var response = await client.SendAsync(second, TestContext.Current.CancellationToken);

        // Tras borrar, la membresía ya no existe y se conserva la semántica 404.
        Assert.Equal(operation == 4 ? HttpStatusCode.NotFound : HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("W/\"tag\"")]
    [InlineData("\"one\", \"two\"")]
    [InlineData("invalid")]
    public async Task ConditionalMutationInvalidHeaderShouldReturnPreconditionFailed(string tag)
    {
        using var client = _app.GetTestClient();
        using var request = CreateRequest(0);
        request.Headers.TryAddWithoutValidation("If-Match", tag);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task ConditionalMutationEmptyHeaderShouldReturnPreconditionRequired()
    {
        using var client = _app.GetTestClient();
        using var request = CreateRequest(0);
        request.Headers.TryAddWithoutValidation("If-Match", " ");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Fact]
    public async Task ConditionalMutationMultipleTagsShouldReturnPreconditionFailed()
    {
        using var client = _app.GetTestClient();
        var tag = await ReadTagAsync(client, 0);
        using var request = CreateRequest(0);
        request.Headers.TryAddWithoutValidation("If-Match", new[] { tag, tag });

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task ConditionalMutationPaddedStrongTagShouldSucceed()
    {
        using var client = _app.GetTestClient();
        var tag = await ReadTagAsync(client, 0);
        using var request = CreateRequest(0);
        request.Headers.TryAddWithoutValidation("If-Match", $" {tag} ");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConditionalMutationOtherTypeSameIdentityShouldReturnPreconditionFailed(int operation)
    {
        using var client = _app.GetTestClient();
        using var request = CreateRequest(operation);
        var resource = operation == 0 ? _seedUserId : _seedTargetId;
        var type = operation == 0 ? IdentityEntityTagResource.Membership : IdentityEntityTagResource.User;
        request.Headers.TryAddWithoutValidation("If-Match", _protector.Create(_seedTenantId, type, resource, 1));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentTenantWritesSameVersionShouldReturnOnePreconditionFailureAtSave()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var client = _app.GetTestClient();
        var tag = await ReadTagAsync(client, 2);
        using var first = CreateRequest(2);
        using var second = CreateRequest(2);
        first.Headers.TryAddWithoutValidation("If-Match", tag);
        second.Headers.TryAddWithoutValidation("If-Match", tag);
        _saveGate.Enabled = true;

        var responses = await Task.WhenAll(client.SendAsync(first, timeout.Token), client.SendAsync(second, timeout.Token));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.PreconditionFailed },
            responses.Select(response => response.StatusCode).Order().ToArray());
    }

    private HttpRequestMessage CreateRequest(int operation) => operation switch
    {
        0 => Put("users/me", "{\"firstName\":\"Changed\",\"lastName\":\"Owner\",\"phone\":null}"),
        1 => Put("tenants/me", "{\"name\":\"Changed Tenant\",\"slug\":\"changed-tenant\",\"planId\":\"starter\"}"),
        2 => Put("tenants/me/settings", "{\"administration\":10,\"profit\":10,\"quality\":1,\"safetyHealth\":1,\"environment\":1,\"contingency\":1}"),
        3 => Put($"users/{_seedTargetId}/role", "{\"role\":2}"),
        _ => new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/identity/users/{_seedTargetId}")
    };

    private static HttpRequestMessage Put(string path, string body) => new(HttpMethod.Put, $"/api/v1/identity/{path}")
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private async Task<string> ReadTagAsync(HttpClient client, int operation)
    {
        var path = operation switch { 0 => "users/me", 1 or 2 => "tenants/me", _ => "users" };
        using var response = await client.GetAsync(new Uri($"/api/v1/identity/{path}", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        if (operation < 3)
        {
            return response.Headers.ETag!.ToString();
        }
        using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        return json.RootElement.GetProperty("data").EnumerateArray()
            .Single(user => user.GetProperty("userId").GetGuid() == _seedTargetId)
            .GetProperty("membershipETag").GetString()!;
    }

    private sealed record TestTenantContext(Guid TenantId, Guid UserId) : ITenantContext
    {
        public bool IsAuthenticated => true;
    }

    private sealed class ConcurrentSaveGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothSaving = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        internal bool Enabled { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                if (Interlocked.Increment(ref _arrivals) == 2)
                {
                    _bothSaving.TrySetResult();
                }
                await _bothSaving.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            return result;
        }
    }
}
